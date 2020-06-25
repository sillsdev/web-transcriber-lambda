using System;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.AspNetCore.Http;
using SIL.Transcriber.Utility;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using SIL.Auth.Models;
using SIL.Paratext.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;

namespace SIL.Transcriber.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        private const string EMAIL_PATTERN = "^[a-zA-Z0-9.+_-]+@[a-zA-Z0-9.-]+[.]+[a-zA-Z]{2,}$";
        public HttpContext HttpContext { get; set; }
        public Auth0ManagementApiTokenService TokenService { get; set; }

        private string auth0Id;
        private string authType;
        private ManagementApiClient managementApiClient;
        private User auth0User;
        protected ILogger<ICurrentUserContext> Logger { get; set; }

        public HttpCurrentUserContext(
             ILoggerFactory loggerFactory,
             IHttpContextAccessor httpContextAccessor,
             Auth0ManagementApiTokenService tokenService)
        {
            HttpContext = httpContextAccessor.HttpContext;
            TokenService = tokenService;
            Logger = loggerFactory.CreateLogger<ICurrentUserContext>();
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

        private User Auth0User
        {
            get
            {
                if (auth0User == null)
                {
                    try
                    {
                        //auth0User = ManagementApiClient.Users.GetAsync(Auth0Id, "user_metadata", true).Result;

                        auth0User = ManagementApiClient.Users.GetAsync(Auth0Id).Result;

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

        /*
        private SILAuth_User SILUser
        {
            get
            {
                if (silUser == null)
                {
                    try
                    {
                        silUser = SILIdentity.GetUser(Auth0Id);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message == "Not Found")
                        {
                            silUser = SILIdentity.CreateUser(Name, GivenName, FamilyName, Email, Auth0Id);
                        }
                        else
                        {
                            throw ex;
                        }
                    }
                }
                return silUser;
            }
        }
        public List<SILAuth_Organization> SILOrganizations
        {
            get
            {
                return SILIdentity.GetOrganizations(SILUser.id);
            }
        }
        */
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
                return Auth0User.Email;
            }
        }

        public string GivenName
        {
            get
            {
                return Auth0User.FirstName;  //HttpContext.GetAuth0GivenName() ;
            }
        }

        public string FamilyName
        {
            get
            {
                return Auth0User.LastName;  //HttpContext.GetAuth0SurName();
            }
        }

        public string Name
        {
            get
            {
                return Auth0User.FullName;  //HttpContext.GetAuth0Name();
            }
        }
        public string Avatar
        {
            get
            {
                return Auth0User.Picture;  //HttpContext.GetAuth0Name();
            }
        }
        public bool EmailVerified
        {
            get
            {
                return Auth0User.EmailVerified ?? false;  //HttpContext.GetAuth0Name();
            }
        }
        /*
        public int SilUserid
        {
            get
            {
                return SILUser.id;
            }
        }
        */
        public UserSecret ParatextLogin(string connection, int userId)
        {
            var identities = Auth0User.Identities;
            var ptIdentity = identities.FirstOrDefault(i => i.Connection == connection); //i.e. "Paratext-Transcriber"
            if (ptIdentity != null)
            {
                var newPTTokens = new ParatextToken
                {
                    AccessToken = (string)ptIdentity.AccessToken,
                    RefreshToken = (string)ptIdentity.RefreshToken,
                    OriginalRefreshToken = (string)ptIdentity.RefreshToken,
                    UserId = userId
                };
                Logger.LogInformation("Http ParatextRefreshToken: {0} ", newPTTokens.RefreshToken);
                return new UserSecret
                {
                    ParatextTokens = newPTTokens
                };
            }
            Logger.LogInformation("Paratext Login - no connection");
            return null;
        }
    }
}
