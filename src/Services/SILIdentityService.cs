using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Auth.Models;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Services
{
    public interface ISILIdentityService
    {
        /*
        //06/2021 teams dropped from identity
        Organization GetOrganization(string teamId);
        List<SILId_Team> GetTeams();
        Organization OrgFromSILAuth(SILId_Team entity);
        Organization OrgFromSILAuth(Organization newEntity, SILId_Team entity);
        SILId_Team CreateTeam(string teamName);
        string CreateInvite(string email, string teamGuid);
        SILAuth_User CreateUser(string name, string givenName, string familyName, string email, string externalId);
        */
        SILAuth_User GetUser(string Auth0Id);
        SILAuth_User UpdateUser(SIL.Transcriber.Models.User user);
     }

    public class SILIdentityService : ISILIdentityService
    {
        private HttpContext HttpContext;
        private HttpClient silAuthClient;
        private UserService userService;
        protected ILogger<SILIdentityService> Logger { get; set; }
        public SILIdentityService(
            IHttpContextAccessor httpContextAccessor,
            UserService UserService,
            ILoggerFactory loggerFactory)
        {
            HttpContext = httpContextAccessor.HttpContext;
            silAuthClient = new HttpClient();
            Uri domainUri = new Uri(GetVarOrThrow("SIL_TR_SILID_DOMAIN"));
            silAuthClient.BaseAddress = domainUri;
            string jwt = HttpContext.GetJWT().Result;
            silAuthClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
            this.Logger = loggerFactory.CreateLogger<SILIdentityService>();
            userService = UserService;
        }
        /*
        //06/2021 teams dropped from identity
        public Organization OrgFromSILAuth(Organization newEntity, SILId_Team entity)
        {
            //newEntity.SilGuid = entity.id;
            newEntity.Name = entity.name;
            return newEntity;
        }
        //06/2021 ready
        public Organization OrgFromSILAuth(SILId_Team entity)
        {
            return OrgFromSILAuth(new Organization(), entity);
        }
        //06/2021 ready
        public Organization GetOrganization(string teamGuid)
        {
            HttpResponseMessage response = silAuthClient.GetAsync("team/" + teamGuid.ToString()).Result;
            if (response.IsSuccessStatusCode)
            {
                string jsonData = response.Content.ReadAsStringAsync().Result;
                return OrgFromSILAuth(JsonConvert.DeserializeObject<SILId_Team>(jsonData));
            }
            else
            {
                throw new Exception(response.ReasonPhrase);
            }
        }
        //06/2021 ready
        public List<SILId_Team> GetTeams()
        {
            HttpResponseMessage response = silAuthClient.GetAsync("team").Result;
            if (response.IsSuccessStatusCode)
            {
                string jsonData = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<List<SILId_Team>>(jsonData);
            }
            else
            {
                throw new Exception(response.ReasonPhrase);
            }
        }
        //06/2021 ready
        public SILId_Team CreateTeam(string teamName)
        {
            User user = userService.GetCurrentUser();
            JObject reqObj = new JObject(
               new JProperty("name", teamName),
               new JProperty("leader", user.Email));

             HttpResponseMessage response = silAuthClient.PostAsync("team", new StringContent(reqObj.ToString(), Encoding.UTF8, "application/json")).Result;
             if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);
            string jsonData = response.Content.ReadAsStringAsync().Result;
            SILId_Team team = JsonConvert.DeserializeObject<SILId_Team>(jsonData);
            return team;
        } */
        //06/2021 - update to bcp-47
        public SILAuth_User GetUser(string Auth0Id)
        {
            HttpResponseMessage response = silAuthClient.GetAsync("agent/" + Auth0Id).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);
            string jsonData = response.Content.ReadAsStringAsync().Result;
            SILAuth_User user = JsonConvert.DeserializeObject<SILAuth_User>(jsonData);
            if (user.user_metadata.ContainsKey("silLocale"))
                user.silLocale = user.user_metadata["silLocale"]["iso6393"].ToString();
            if (user.user_metadata.ContainsKey("zoneinfo"))
                user.zoneinfo = user.user_metadata["zoneinfo"]["name"].ToString();

            return user;
        }
        //06/2021 ready - update to bcp-47
        public SILAuth_User UpdateUser(User user)
        {
            SILAuth_User authuser = GetUser(user.ExternalId);
            Debug.WriteLine(authuser);
            JObject requestObj = new JObject();
            if (authuser.name != user.Name)
                requestObj.Add("name", user.Name);
            if (authuser.given_name != user.GivenName)
                requestObj.Add("given_name", user.GivenName);
            if (authuser.family_name != user.FamilyName)
                requestObj.Add("family_name", user.FamilyName);
            if (requestObj.HasValues && !authuser.user_id.StartsWith("google"))
            {
                HttpResponseMessage response = silAuthClient.PatchAsync("agent/" + authuser.user_id, new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;

                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.ReasonPhrase);
            }

            if (authuser.silLocale != user.Locale)
            {
                string locale = user.Locale == "en" ? "eng" : "tlh"; //TEMP!!
                HttpResponseMessage response = silAuthClient.PutAsync("locale/" + locale, new StringContent("", Encoding.UTF8, "application/json")).Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Locale update error: " + locale + " " + response.ReasonPhrase);
            } 
           
            if (authuser.zoneinfo != user.Timezone)
            {
                JObject reqObj = new JObject(
                new JProperty("timezone", user.Timezone));

                HttpResponseMessage response = silAuthClient.PutAsync("timezone/" + authuser.user_id, new StringContent(reqObj.ToString(), Encoding.UTF8, "application/json")).Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception("Timezone update error: " + user.Timezone + " " + response.ReasonPhrase);
            }
            return GetUser(user.ExternalId);
        }
        /*
        //06/2021 I don't think we can do this in the current identity
        public SILAuth_User CreateUser(string name, string givenName, string familyName, string email, string externalId)
        {
            JObject requestObj = new JObject(
                new JProperty("name ", name),
                new JProperty("givenname ", givenName),
                new JProperty("familyname ",familyName),
                new JProperty("email  ", email),
                new JProperty("externalid  ", externalId)
                );

            //call the Identity api and receive an invitation id
            HttpResponseMessage response = silAuthClient.PostAsync("user", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);

            string jsonData = response.Content.ReadAsStringAsync().Result;
           return JsonConvert.DeserializeObject<SILAuth_User>(jsonData);
        }
        /*
        /*
        public string CreateInvite(string email, string teamGuid)
        {
            JObject requestObj = new JObject(
                new JProperty("email", email));


            //call the Identity api and receive an invitation id
            HttpResponseMessage response = silAuthClient.PutAsync("team/"+teamGuid+"/agent", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);

            string jsonData = response.Content.ReadAsStringAsync().Result;
            SILId_Invite invite = JsonConvert.DeserializeObject<SILId_Invite>(jsonData);

            return invite.uuid;
        }
        */
    }
}
