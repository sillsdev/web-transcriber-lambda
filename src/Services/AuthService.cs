using Auth0.ManagementApi;
using Auth0.ManagementApi.Jobs;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    /// <summary>
    /// This service provides methods for accessing the Auth0 Management API.
    /// </summary>
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private string? _accessToken;
        private ManagementClient? managementApiClient;

        public AuthService()
        {
            string domain = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
            if (!domain.EndsWith('/'))
                domain += "/";
            _httpClient = new HttpClient { BaseAddress = new Uri(domain) };
        }

        private ManagementClient ManagementApiClient {
            get {
                if (managementApiClient == null)
                {
                    Uri domainUri = new(GetVarOrThrow("SIL_TR_AUTH0_DOMAIN"));
                    managementApiClient = new ManagementClient(new ManagementClientOptions
                    {
                        Domain = domainUri.Host,
                        TokenProvider = new DelegateTokenProvider(async _ =>
                            (await GetAccessTokenAsync()).AccessToken ?? ""),
                    });
                }
                return managementApiClient;
            }
        }

        public async Task<GetUserResponseContent> GetUserAsync(string Auth0Id) =>
            await ManagementApiClient.Users.GetAsync(Auth0Id, new GetUserRequestParameters());

        public async Task ResendVerification(string authId)
        {
            CreateVerificationEmailRequestContent content = new()
            { UserId = authId };
            _ = await ManagementApiClient.Jobs.VerificationEmail.CreateAsync(content);
        }

        private async Task<(string? AccessToken, bool Refreshed)> GetAccessTokenAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!IsAccessTokenExpired())
                    return (_accessToken, false);

                string clientId = GetVarOrDefault("SIL_TR_AUTH0_TOKEN_ACCESS_CLIENT_ID", "");
                string clientSecret = GetVarOrDefault(
                    "SIL_TR_AUTH0_TOKEN_ACCESS_CLIENT_SECRET",
                    ""
                );
                HttpRequestMessage request = new(HttpMethod.Post, "oauth/token");

                JObject requestObj =
                    new(
                        new JProperty("grant_type", "client_credentials"),
                        new JProperty("client_id", clientId),
                        new JProperty("client_secret", clientSecret),
                        new JProperty("audience", _httpClient.BaseAddress + "api/v2/")
                    );
                request.Content = new StringContent(
                    requestObj.ToString(),
                    Encoding.UTF8,
                    "application/json"
                );
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                _ = response.EnsureSuccessStatusCode();
                string responseJson = await response.Content.ReadAsStringAsync();
                dynamic responseObj = JObject.Parse(responseJson);
                _accessToken = (string)responseObj.access_token;
                return (_accessToken, true);
            }
            finally
            {
                _ = _lock.Release();
            }
        }

        private bool IsAccessTokenExpired()
        {
            if (_accessToken == null)
                return true;
            JwtSecurityToken accessToken = new(_accessToken);
            DateTime now = DateTime.UtcNow;
            return now < accessToken.ValidFrom || now > accessToken.ValidTo;
        }
    }
}
