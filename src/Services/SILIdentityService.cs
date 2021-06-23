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
        Organization GetOrganization(int orgId);
        List<SILAuth_Organization> GetOrganizations();
        List<SILAuth_Organization> GetOrganizations(int silUserId);
        Organization OrgFromSILAuth(SILAuth_Organization entity);
        Organization OrgFromSILAuth(Organization newEntity, SILAuth_Organization entity);
        SILAuth_User GetUser(string Auth0Id);
        SILAuth_User UpdateUser(SIL.Transcriber.Models.User user);
        SILAuth_User CreateUser(string name, string givenName, string familyName, string email, string externalId);
        int CreateInvite(string email, string orgName, int silOrgId, int silUserId);
    }
    public class SILIdentityService : ISILIdentityService
    {
        private HttpContext HttpContext;
        private HttpClient silAuthClient;
        protected ILogger<SILIdentityService> Logger { get; set; }
        public SILIdentityService(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory)
        {
            HttpContext = httpContextAccessor.HttpContext;
            silAuthClient = new HttpClient();
            Uri domainUri = new Uri(GetVarOrThrow("SIL_TR_SILID_DOMAIN"));
            silAuthClient.BaseAddress = domainUri;
            var jwt = HttpContext.GetJWT().Result;
            silAuthClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", jwt);
            this.Logger = loggerFactory.CreateLogger<SILIdentityService>();
        }

        public Organization OrgFromSILAuth(Organization newEntity, SILAuth_Organization entity)
        {
            newEntity.SilId = entity.id;
            newEntity.Name = entity.name;
            newEntity.LogoUrl = entity.logo;
            newEntity.Description = entity.description;
            newEntity.WebsiteUrl = entity.websiteurl;
            return newEntity;
        }

        public Organization OrgFromSILAuth(SILAuth_Organization entity)
        {
            return OrgFromSILAuth(new Organization(), entity);
        }

        public Organization GetOrganization(int orgId)
        {
            HttpResponseMessage response = silAuthClient.GetAsync("organizations/" + orgId.ToString()).Result;
            if (response.IsSuccessStatusCode)
            {
                string jsonData = response.Content.ReadAsStringAsync().Result;
                return OrgFromSILAuth(JsonConvert.DeserializeObject<SILAuth_Organization>(jsonData));
            }
            else
            {
                throw new Exception(response.ReasonPhrase);
            }
        }
        public List<SILAuth_Organization> GetOrganizations()
        {
            HttpResponseMessage response = silAuthClient.GetAsync("organizations").Result;
            if (response.IsSuccessStatusCode)
            {
                string jsonData = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<List<SILAuth_Organization>>(jsonData);
            }
            else
            {
                throw new Exception(response.ReasonPhrase);
            }
        }

        public List<SILAuth_Organization> GetOrganizations(int SILUser)
        {
            HttpResponseMessage response = silAuthClient.GetAsync("memberships").Result;
            if (response.IsSuccessStatusCode)
            {
                string jsonData = response.Content.ReadAsStringAsync().Result;
                List<SILAuth_Membership> memberships = JsonConvert.DeserializeObject<List<SILAuth_Membership>>(jsonData);
                memberships = memberships.FindAll(m => m.userId == SILUser);
                string silOrgs = "|";
                memberships.ForEach(m => silOrgs += m.orgId.ToString() + "|");

                List<SILAuth_Organization> orgs = GetOrganizations();
                return orgs.FindAll(o => silOrgs.Contains("|" + o.id.ToString() + "|"));
            }
            else
            {
                throw new Exception(response.ReasonPhrase);
            }
        }
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
            if (requestObj.HasValues)
            {
                HttpResponseMessage response = silAuthClient.PatchAsync("agent/" + authuser.user_id, new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;

                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.ReasonPhrase);
            }
            if (authuser.silLocale != user.Locale)
            {
                HttpResponseMessage response = silAuthClient.PatchAsync("locale/" + user.Locale, new StringContent("{}", Encoding.UTF8, "application/json")).Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.ReasonPhrase);
            }
            if (authuser.zoneinfo != user.Timezone)
            {
                JObject reqObj = new JObject(
                new JProperty("timezone ", user.Timezone));

                HttpResponseMessage response = silAuthClient.PatchAsync("timezone/" + authuser.user_id, new StringContent(reqObj.ToString(), Encoding.UTF8, "application/json")).Result;
                if (!response.IsSuccessStatusCode)
                    throw new Exception(response.ReasonPhrase);
            }
            return GetUser(user.ExternalId);
        }
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
        public int CreateInvite(string email, string orgName, int silOrgId, int silUserId)
        {
            JObject requestObj = new JObject(
                new JProperty("email", email),
                new JProperty("orgId", silOrgId),
                new JProperty("userId", silUserId));

            //call the Identity api and receive an invitation id
            HttpResponseMessage response = silAuthClient.PostAsync("invite", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);

            string jsonData = response.Content.ReadAsStringAsync().Result;
            SILAuth_Invite invite = JsonConvert.DeserializeObject<SILAuth_Invite>(jsonData);

            requestObj = new JObject(
                new JProperty("email", email),
                new JProperty("orgName", orgName),
                new JProperty("inviteId", invite.id));

            //tell the Identity api to send the email (duh...it should have done that above)
            silAuthClient.PostAsync("sendEmail", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json"));
            return invite.id;
        }
    }
}
