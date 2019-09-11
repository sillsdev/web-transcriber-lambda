using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Utility
{
    public static class SILIdentity
    {
        public static HttpClient SILAuthApiClient(Auth0ManagementApiTokenService TokenService)
        {
            HttpClient silAuthClient = new HttpClient();

            var domainUri = new Uri(GetVarOrThrow("SIL_TR_SILAUTH_API"));
            var token = TokenService.SILAuthToken; //soon come...use my own token...
            silAuthClient.BaseAddress = domainUri;
            silAuthClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return silAuthClient;
        }

        public static string GetData(string response)
        {
            //find the data
            const string search = "\"data\":";
            string data = response.Substring(response.IndexOf(search) + search.Length);

            return data.Remove(data.Length - 1);
        }
    }
}
