using System;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using RestSharp;
using Newtonsoft.Json.Linq;
using Serilog;

namespace SIL.Transcriber.Services
{
    /// <summary>
    /// Auth0 Managment API requires that a token is used to access the API.
    /// By default, the tokens expire after 24 hours.
    ///
    /// The documented method for automating the generation of these tokens
    /// in C# is to use the Rest API.
    /// https://auth0.com/docs/api/management/v2/tokens#automate-the-process
    ///
    /// This class depends on the following environment variables:
    /// * AUTH0_DOMAIN - Url (e.g. https://YOUR_APPLICATION.auth0.com
    /// * AUTH0_TOKEN_ACCESS_CLIENT_ID - 'Client ID' value from the Machine to
    ///   Machine Applications settings
    /// * AUTH0_TOKEN_ACCESS_CLIENT_SECRET - 'Client Secret' from the Machine to
    ///   Machine Applications settings
    /// </summary>
    public class Auth0ManagementApiTokenService
    {
        public  string tokenAuth;
        private string tokenSIL;
        public Auth0ManagementApiTokenService()
        {
        }
        private string token(string audience)
        {
            var domainUrl = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
            if (!domainUrl.EndsWith("/")) domainUrl += "/";
            var clientId = GetVarOrDefault("SIL_TR_AUTH0_TOKEN_ACCESS_CLIENT_ID", "");
            var clientSecret = GetVarOrDefault("SIL_TR_AUTH0_TOKEN_ACCESS_CLIENT_SECRET", "");
            var client = new RestClient($"{domainUrl}oauth/token");
            var request = new RestRequest(Method.POST);
            request.AddHeader("content-type", "application/json");
            request.AddParameter("application/json", $"{{\"grant_type\":\"client_credentials\",\"client_id\": \"{clientId}\",\"client_secret\": \"{clientSecret}\",\"audience\": \"{audience}\"}}", ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            if (response.IsSuccessful)
            {
                dynamic json = JObject.Parse(response.Content);
                return json.access_token;
            }
            else
            {
                Log.Error($"Failed to request token from Auth0: Status={response.StatusDescription}, Content={response.Content}");
                return "";
            }
        }

        public string Token
        {
            get
            {
                if (tokenAuth == null)
                {
                    var domainUrl = GetVarOrThrow("SIL_TR_AUTH0_DOMAIN");
                    if (!domainUrl.EndsWith("/")) domainUrl += "/";
                    tokenAuth = token(domainUrl + "api/v2/");
                }
                return tokenAuth;
            }
        }
        public string SILAuthToken
        {
            get
            {
                if (tokenSIL == null)
                {
                    //var domainUrl = GetVarOrThrow("SIL_TR_SILAUTH_DOMAIN"); TODO
                    //tokenSIL = token(domainUrl);
                    tokenSIL = token("https://transcriber-auth");
                }
                Console.WriteLine("SIL Auth token received.");
                return tokenSIL;
            }
        }
    }
}
