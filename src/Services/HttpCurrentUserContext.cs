using System;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.AspNetCore.Http;
using SIL.Transcriber.Utility;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using SIL.Auth.Models;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;


namespace SIL.Transcriber.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        public HttpContext HttpContext { get; set; }
        public Auth0ManagementApiTokenService TokenService { get; set; }

        private string auth0Id;
        private string authType;
        private ManagementApiClient managementApiClient;
        private HttpClient silAuthClient;
        private User auth0User;
        private SILAuth_User silUser;

        public HttpCurrentUserContext(
            IHttpContextAccessor httpContextAccessor,
            Auth0ManagementApiTokenService tokenService)
        {
            HttpContext = httpContextAccessor.HttpContext;
            TokenService = tokenService;
        }

        private string AuthType
        {
            get
            {
                if (authType == null)
                {
                    authType = this.HttpContext.GetAuth0Type();
                }
                return authType;
            }
        }

        private ManagementApiClient ManagementApiClient
        {
            get
            {
                if (managementApiClient == null)
                {
                    var token = TokenService.Token;
                    var domainUri = new Uri(GetVarOrThrow("SIL_TR_AUTH0_DOMAIN"));
                    managementApiClient = new ManagementApiClient(token, domainUri.Host);
                }
                return managementApiClient;
            }
        }
        private HttpClient SILAuthApiClient
        {
            get
            {
                if (silAuthClient == null)
                {
                   silAuthClient = SILIdentity.SILAuthApiClient(TokenService);
                }
                return silAuthClient;
            }
        }
        private User Auth0User
        {
            get
            {
                if (auth0User == null)
                {
                    try
                    {
                        auth0User = ManagementApiClient.Users.GetAsync(Auth0Id, "user_metadata", true).Result;

                        //auth0User = ManagementApiClient.Users.GetAsync(Auth0Id).Result;

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        throw;
                    }
                }
                return auth0User;
            }
        }

        private SILAuth_User SILUser
        {
            get
            {
                if (silUser == null)
                {
                    HttpResponseMessage response = SILAuthApiClient.GetAsync("user/" + Auth0Id).Result;
                    if (!response.IsSuccessStatusCode)
                        throw new Exception(response.ReasonPhrase);

                    var jsonData = SILIdentity.GetData(response.Content.ReadAsStringAsync().Result);
                        List<SILAuth_User> users = JsonConvert.DeserializeObject<List<SILAuth_User>>(jsonData);
                        silUser = users[0];
                }
                return silUser;
            }
        }

        public List<SILAuth_Organization> SILOrganizations
        {
            get
            {
                List<SILAuth_Organization> orgs = null;
                HttpResponseMessage response = SILAuthApiClient.GetAsync("memberships").Result;
                if (response.IsSuccessStatusCode)
                {
                    var jsonData = SILIdentity.GetData(response.Content.ReadAsStringAsync().Result);
                    try
                    {
                        List<SILAuth_Membership> memberships = JsonConvert.DeserializeObject<List<SILAuth_Membership>>(jsonData);
                        memberships = memberships.FindAll(m => m.userId == SILUser.id);
                        string silOrgs = "|";
                        memberships.ForEach(m => silOrgs += m.orgId.ToString() + "|");

                        response = SILAuthApiClient.GetAsync("organizations").Result;
                        jsonData = SILIdentity.GetData(response.Content.ReadAsStringAsync().Result);
                        orgs = JsonConvert.DeserializeObject<List<SILAuth_Organization>>(jsonData);
                        orgs = orgs.FindAll(o => silOrgs.Contains("|" + o.id.ToString() + "|"));
                    }
                    catch (Exception ex)
                    {

                        Console.WriteLine(ex.Message);
                    }
                }
                return orgs;
            }
        }
        public string Auth0Id
        {
            get
            {
                if (auth0Id == null)
                {
                    auth0Id = this.HttpContext.GetAuth0Id();
                }
                return auth0Id;
            }
        }

        public string Email
        {
            get
            {
                return SILUser.email; ;
                /*
                if (email == null)
                {
                    
                    email = this.HttpContext.GetAuth0Email();
                    if (email is null)
                        email = Auth0User.Email;
                    
            }
                return email;*/
            }
        }

        public string GivenName
        {
            get
            {
                return SILUser.givenname;
                /*
                if (givenName == null)
                {
                    /*
                    var auth = AuthType;
                    if (auth.StartsWith("auth0", StringComparison.Ordinal))
                    {
                        try
                        {
                            // Use Auth0 Management API to get value
                            if (Auth0User.UserMetadata != null)
                                givenName = Auth0User.UserMetadata.given_name;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to request given_name from Auth0: auth0id={Auth0Id}");
                        }
                    }
                    else
                    {
                        givenName = this.HttpContext.GetAuth0GivenName();
                    }
                    
                }
                return givenName; */
            }
        }

        public string FamilyName
        {
            get
            {
                return SILUser.familyname;
                /*
                if (familyName == null)
                {
                    
                    /*
                    var auth = AuthType;
                    if (auth.StartsWith("auth0", StringComparison.Ordinal))
                    {
                        try
                        {
                            // Use Auth0 Management API to get value
                            if (Auth0User.UserMetadata != null)
                                familyName = Auth0User.UserMetadata.family_name;
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, $"Failed to request family_name from Auth0: auth0id={Auth0Id}");
                        }
                    }
                    else
                    {
                        familyName = this.HttpContext.GetAuth0SurName();
                    }
                    
                }
                return familyName;
                */
            }
        }

        public string Name
        {
            get
            {
                return SILUser.nickname;
                /*
                if (name == null)
                {
                    name = this.HttpContext.GetAuth0Name();
                }
                return name;
                */
            }
        }
        public int SilUserid
        {
            get
            {
                return SILUser.id;
            }
        }

    }
}
