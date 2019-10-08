using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Auth.Models;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
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
        int CreateInvite(string email, string orgName, int silOrgId, int silUserId);
    }
    public class SILIdentityService : ISILIdentityService
    {
        private HttpContext HttpContext;
        private HttpClient silAuthClient;

        public SILIdentityService(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory)
        {
            HttpContext = httpContextAccessor.HttpContext;
            silAuthClient = new HttpClient();
            var domainUri = new Uri(GetVarOrThrow("SIL_TR_SILAUTH_API"));
            silAuthClient.BaseAddress = domainUri;
            silAuthClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", HttpContext.GetJWT().Result);
        }

        private string GetData(string response)
        {
            //find the data
            const string search = "\"data\":";
            string data = response.Substring(response.IndexOf(search) + search.Length);

            return data.Remove(data.Length - 1);
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
                var jsonData = GetData(response.Content.ReadAsStringAsync().Result);
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
                var jsonData = GetData(response.Content.ReadAsStringAsync().Result);
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
                var jsonData = GetData(response.Content.ReadAsStringAsync().Result);
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
            HttpResponseMessage response = silAuthClient.GetAsync("user/" + Auth0Id).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);

            var jsonData = GetData(response.Content.ReadAsStringAsync().Result);
            List<SILAuth_User> users = JsonConvert.DeserializeObject<List<SILAuth_User>>(jsonData); //because of bad data it could be a list
            return users[0];
        }
        public int CreateInvite(string email, string orgName, int silOrgId, int silUserId)
        {
            var requestObj = new JObject(
                new JProperty("email", email),
                new JProperty("orgId", silOrgId),
                new JProperty("userId", silUserId));

            //call the Identity api and receive an invitation id
            HttpResponseMessage response = silAuthClient.PostAsync("invite", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);

            var jsonData = GetData(response.Content.ReadAsStringAsync().Result);
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
