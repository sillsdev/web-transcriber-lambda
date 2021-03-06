using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Newtonsoft.Json.Linq;
using RestSharp;
using SIL.ObjectModel;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    /// <summary>
    /// This service provides methods for accessing the Auth0 Management API.
    /// </summary>
    public class AuthService : DisposableBase, IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private string _accessToken;
        private ManagementApiClient managementApiClient;


        public AuthService()
        {
            string domain = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
            if (!domain.EndsWith("/")) domain += "/";
            _httpClient = new HttpClient
            {
                BaseAddress =new Uri(domain)
            };
        }

        public bool ValidateWebhookCredentials(string username, string password)
        {
            return GetVarOrThrow("SIL_TR_WEBHOOK_USERNAME") == username && GetVarOrThrow("SIL_TR_WEBHOOK_PASSWORD") == password;
        }

        private ManagementApiClient ManagementApiClient
        {
            get
            {
                if (managementApiClient == null)
                {
                    (string AccessToken, bool Refreshed) = GetAccessTokenAsync().Result;
                    string accessToken = AccessToken;
                    Uri domainUri = new Uri(GetVarOrThrow("SIL_TR_AUTH0_DOMAIN"));
                    managementApiClient = new ManagementApiClient(accessToken, domainUri.Host);
                }
                return managementApiClient;
            }
        }

        public async Task<User> GetUserAsync(string Auth0Id)
        {
                //auth0User = ManagementApiClient.Users.GetAsync(Auth0Id, "user_metadata", true).Result;
            return await ManagementApiClient.Users.GetAsync(Auth0Id);
        }

        public Task ResendVerification(string authId)
        {
            VerifyEmailJobRequest content = new VerifyEmailJobRequest
            {
                UserId = authId
            };
            return ManagementApiClient.Jobs.SendVerificationEmailAsync(content);
        }

        private async Task<(string AccessToken, bool Refreshed)> GetAccessTokenAsync()
        {
            await _lock.WaitAsync();
            try
            {
                if (!IsAccessTokenExpired())
                    return (_accessToken, false);

                string clientId = GetVarOrDefault("SIL_TR_AUTH0_TOKEN_ACCESS_CLIENT_ID", "");
                string clientSecret = GetVarOrDefault("SIL_TR_AUTH0_TOKEN_ACCESS_CLIENT_SECRET", "");
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "oauth/token");

                JObject requestObj = new JObject(
                    new JProperty("grant_type", "client_credentials"),
                    new JProperty("client_id", clientId),
                    new JProperty("client_secret", clientSecret),
                    new JProperty("audience", _httpClient.BaseAddress + "api/v2/"));
                request.Content = new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                string responseJson = await response.Content.ReadAsStringAsync();
                dynamic responseObj = JObject.Parse(responseJson);
                _accessToken = (string)responseObj.access_token;
                return (_accessToken, true);
            }
            finally
            {
                _lock.Release();
            }
        }

        private bool IsAccessTokenExpired()
        {
            if (_accessToken == null)
                return true;
            JwtSecurityToken accessToken = new JwtSecurityToken(_accessToken);
            DateTime now = DateTime.UtcNow;
            return now < accessToken.ValidFrom || now > accessToken.ValidTo;
        }

        protected override void DisposeManagedResources()
        {
            _httpClient.Dispose();
        }
    }
}
