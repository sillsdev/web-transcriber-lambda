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
        //private ManagementApiClient managementApiClient;
        private ISILIdentityService SILIdentity;
        public AuthService(ISILIdentityService silIdentity)
        {
            string domain = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
            if (!domain.EndsWith("/")) domain += "/";
            _httpClient = new HttpClient
            {
                BaseAddress =new Uri(domain)
            };
            SILIdentity = silIdentity;
        }

        public bool ValidateWebhookCredentials(string username, string password)
        {
            return GetVarOrThrow("SIL_TR_WEBHOOK_USERNAME") == username && GetVarOrThrow("SIL_TR_WEBHOOK_PASSWORD") == password;
        }

        public Auth.Models.SILAuth_User GetUser(string Auth0Id)
        {
            return SILIdentity.GetUser(Auth0Id);
        }

        public Task ResendVerification(string authId)
        {
            VerifyEmailJobRequest content = new VerifyEmailJobRequest
            {
                UserId = authId
            };
        throw new Exception("not implemented");
        }

        protected override void DisposeManagedResources()
        {
            _httpClient.Dispose();
        }
    }
}
