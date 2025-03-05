using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Controllers
{
    public class BaseController<T>(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<T, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<T, int>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            ) where T : class, IIdentifiable<int>
    {
    }

    public class BaseController<T, TId> : JsonApiController<T, TId>
        where T : class, IIdentifiable<TId>
    {
        protected IResourceService<T, TId> Service;
        protected UserService UserService;
        protected ICurrentUserContext CurrentUserContext;
        protected User? _currentUser;
        protected ILogger<T> Logger { get; set; }

        public BaseController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<T, TId> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        ) : base(options, resourceGraph, loggerFactory, resourceService)
        {
            Service = resourceService;
            UserService = userService;
            CurrentUserContext = currentUserContext;
            Logger = loggerFactory.CreateLogger<T>();
            _currentUser = CurrentUser; //make sure this happens first no matter what entrypoint is used
        }

        public User? CurrentUser {
            get {
                if (_currentUser == null && (CurrentUserContext.Auth0Id ?? "") != "")
                {
                    // current user has not yet been found for this request.
                    // find or create because users are managed by auth0 and
                    // creation isn't proxied through the api.
                    _currentUser = FindOrCreateCurrentUser();
                }
                return _currentUser;
            }
        }

        private User? FindOrCreateCurrentUser()
        {
            User? existing = UserService.GetCurrentUser();

            if (existing != null)
            {
                return existing;
            }

            if ((CurrentUserContext.Auth0Id ?? "") == ""
                || CurrentUserContext.Auth0Id == GetVarOrDefault("SIL_TR_WEBHOOK_USERNAME", ""))
            {
                Console.WriteLine("No Auth0 user.");
                return null;
            }

            User newUser =
                new()
                {
                    ExternalId = CurrentUserContext.Auth0Id,
                    Email = CurrentUserContext.Email,
                    Name = CurrentUserContext.Name,
                    GivenName = CurrentUserContext.GivenName,
                    FamilyName = CurrentUserContext.FamilyName,
                    AvatarUrl = CurrentUserContext.Avatar,
                    DigestPreference = 1, // 0=none, >1=daily  room for future preferences
                    NewsPreference = false,
                    DateCreated = DateTime.UtcNow,
                };
            User? newEntity = UserService.CreateUser(newUser);
            Console.WriteLine("New user created.");

            return newEntity;
        }
    }
}
