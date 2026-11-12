using Auth0.ManagementApi;
using Newtonsoft.Json.Linq;
using SIL.Paratext.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class HttpCurrentUserContext(
        ILoggerFactory loggerFactory,
        IHttpContextAccessor httpContextAccessor,
        IAuthService authService
        ) : ICurrentUserContext
    {
        public HttpContext? HttpContext { get; set; } = httpContextAccessor.HttpContext;
        public IAuthService AuthService { get; set; } = authService;

        private string? auth0Id;

        private GetUserResponseContent? auth0User;
        protected ILogger<ICurrentUserContext> Logger { get; set; } = loggerFactory.CreateLogger<ICurrentUserContext>();

        private GetUserResponseContent Auth0User {
            get {
                auth0User ??= AuthService.GetUserAsync(Auth0Id).Result;
                return auth0User;
            }
        }

        public string Auth0Id {
            get {
                auth0Id ??= HttpContext?.GetAuth0Id() ?? "";
                return auth0Id;
            }
        }

        public string Email {
            get { return Auth0User.Email ?? ""; }
        }

        public string GivenName {
            get { return Auth0User.GivenName ?? ""; }
        }

        public string FamilyName {
            get { return Auth0User.FamilyName ?? ""; }
        }

        public string Name {
            get { return Auth0User.Name ?? ""; }
        }
        public string Avatar {
            get { return Auth0User.Picture ?? ""; }
        }
        public bool EmailVerified {
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
            Logger.LogCritical("Paratext Login - no connection");
            return null;
        }

        public UserSecret? ParatextToken(UserIdentitySchema? ptIdentity, int userId)
        {
            if (ptIdentity != null)
            {
                ParatextToken newPTTokens =
                    new()
                    {
                        AccessToken = ptIdentity.AccessToken ?? "",
                        RefreshToken = ptIdentity.RefreshToken ?? "",
                        OriginalRefreshToken = ptIdentity.RefreshToken ?? "",
                        UserId = userId
                    };
                return new UserSecret { ParatextTokens = newPTTokens };
            }
            Logger.LogCritical("Paratext Login - no connection");
            return null;
        }

        public UserSecret? ParatextLogin(string connection, int userId)
        {
            IEnumerable<UserIdentitySchema>? identities = Auth0User.Identities;
            UserIdentitySchema? ptIdentity = identities?.FirstOrDefault(i => i.Connection == connection); //i.e. "Paratext-Transcriber"
            return ParatextToken(ptIdentity, userId);
        }
    }
}
