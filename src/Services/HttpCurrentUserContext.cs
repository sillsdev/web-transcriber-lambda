using Auth0.ManagementApi.Models;
using SIL.Transcriber.Utility;
using SIL.Paratext.Models;
using Newtonsoft.Json.Linq;

namespace SIL.Transcriber.Services
{
    public class HttpCurrentUserContext : ICurrentUserContext
    {
        public HttpContext? HttpContext { get; set; }
        public IAuthService AuthService { get; set; }

        private string? auth0Id;

        private User? auth0User;
        protected ILogger<ICurrentUserContext> Logger { get; set; }

        public HttpCurrentUserContext(
            ILoggerFactory loggerFactory,
            IHttpContextAccessor httpContextAccessor,
            IAuthService authService
        )
        {
            HttpContext = httpContextAccessor.HttpContext;
            AuthService = authService;
            Logger = loggerFactory.CreateLogger<ICurrentUserContext>();
        }

        private User Auth0User
        {
            get
            {
                if (auth0User == null)
                {
                    try
                    {
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

        public string Auth0Id
        {
            get
            {
                if (auth0Id == null)
                {
                    auth0Id = HttpContext?.GetAuth0Id() ?? "";
                }
                return auth0Id;
            }
        }

        public string Email
        {
            get { return Auth0User.Email; }
        }

        public string GivenName
        {
            get { return Auth0User.FirstName; }
        }

        public string FamilyName
        {
            get { return Auth0User.LastName; }
        }

        public string Name
        {
            get { return Auth0User.FullName; }
        }
        public string Avatar
        {
            get { return Auth0User.Picture; }
        }
        public bool EmailVerified
        {
            get { return Auth0User.EmailVerified ?? false; }
        }

        public UserSecret? ParatextToken(JToken ptIdentity, int userId)
        {
            if (ptIdentity != null)
            {
                ParatextToken newPTTokens =
                    new()
                    {
                        AccessToken = (string?)ptIdentity["AccessToken"] ?? "",
                        RefreshToken = (string?)ptIdentity["RefreshToken"] ?? "",
                        OriginalRefreshToken = (string?)ptIdentity["RefreshToken"] ?? "",
                        UserId = userId
                    };
                return new UserSecret { ParatextTokens = newPTTokens };
            }
            Logger.LogInformation("Paratext Login - no connection");
            return null;
        }

        public UserSecret? ParatextToken(Identity? ptIdentity, int userId)
        {
            if (ptIdentity != null)
            {
                ParatextToken newPTTokens =
                    new()
                    {
                        AccessToken = (string)ptIdentity.AccessToken,
                        RefreshToken = (string)ptIdentity.RefreshToken,
                        OriginalRefreshToken = (string)ptIdentity.RefreshToken,
                        UserId = userId
                    };
                return new UserSecret { ParatextTokens = newPTTokens };
            }
            Logger.LogInformation("Paratext Login - no connection");
            return null;
        }

        public UserSecret? ParatextLogin(string connection, int userId)
        {
            Identity[]? identities = Auth0User.Identities;
            Identity? ptIdentity = identities?.FirstOrDefault(i => i.Connection == connection); //i.e. "Paratext-Transcriber"
            return ParatextToken(ptIdentity, userId);
        }
    }
}
