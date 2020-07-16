using System;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.AspNetCore.Http;
using SIL.Transcriber.Utility;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using SIL.Paratext.Models;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace SIL.Transcriber.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        public HttpContext HttpContext { get; set; }
        public IAuthService AuthService { get; set; }

        private string auth0Id;
        private string authType;

        private User auth0User;
        protected ILogger<ICurrentUserContext> Logger { get; set; }

        public HttpCurrentUserContext(
             ILoggerFactory loggerFactory,
             IHttpContextAccessor httpContextAccessor,
             IAuthService authService)
        {
            HttpContext = httpContextAccessor.HttpContext;
            AuthService = authService;
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

        private User Auth0User
        {
            get
            {
                if (auth0User == null)
                {
                    try
                    {
                        //auth0User = ManagementApiClient.Users.GetAsync(Auth0Id, "user_metadata", true).Result;

                        auth0User = AuthService.GetUserAsync(Auth0Id).Result;

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
                return Auth0User.EmailVerified ?? false; 
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
        public UserSecret ParatextToken(JToken ptIdentity, int userId)
        {
            if (ptIdentity != null)
            {
                ParatextToken newPTTokens = new ParatextToken
                {
                    AccessToken = (string)ptIdentity["AccessToken"],
                    RefreshToken = (string)ptIdentity["RefreshToken"],
                    OriginalRefreshToken = (string)ptIdentity["RefreshToken"],
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
        public UserSecret ParatextToken(Identity ptIdentity, int userId)
        {
            if (ptIdentity != null)
            {
                ParatextToken newPTTokens = new ParatextToken
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
        public UserSecret ParatextLogin(string connection, int userId)
        {
            var identities = Auth0User.Identities;
            Identity ptIdentity = identities.FirstOrDefault(i => i.Connection == connection); //i.e. "Paratext-Transcriber"
            return ParatextToken(ptIdentity, userId);
        }
    }
}
