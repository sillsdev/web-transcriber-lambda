using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Models;
using System.Threading.Tasks;
using System;
using Microsoft.Extensions.Logging;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using Microsoft.Net.Http.Headers;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class BaseController<T> : BaseController<T, int> where T : class, IIdentifiable<int>
    {
        public BaseController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<T, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        )
            : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            ) { }
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BaseController<T, TId> : JsonApiController<T, TId>
        where T : class, IIdentifiable<TId>
    {
        protected IResourceService<T, TId> service;
        protected UserService userService;
        protected ICurrentUserContext currentUserContext;
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
            this.service = resourceService;
            this.userService = userService;
            this.currentUserContext = currentUserContext;
            this.Logger = loggerFactory.CreateLogger<T>();
            _currentUser = CurrentUser; //make sure this happens first no matter what entrypoint is used
        }

        public User? CurrentUser
        {
            get
            {
                if (_currentUser == null) // && HttpContext != null && HttpContext.Request.Headers[HeaderNames.Authorization].Count > 0)
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
            User? existing = userService.GetCurrentUser();

            if (existing != null)
            {
                return existing;
            }

            if (
                currentUserContext.Auth0Id == null
                || currentUserContext.Auth0Id == GetVarOrDefault("SIL_TR_WEBHOOK_USERNAME", "")
            )
            {
                Console.WriteLine("No Auth0 user.");
                return null;
            }

            User newUser =
                new()
                {
                    ExternalId = currentUserContext.Auth0Id,
                    Email = currentUserContext.Email,
                    Name = currentUserContext.Name,
                    GivenName = currentUserContext.GivenName,
                    FamilyName = currentUserContext.FamilyName,
                    AvatarUrl = currentUserContext.Avatar,
                    DigestPreference = 1, // 0=none, >1=daily  room for future preferences
                    NewsPreference = false,
                    DateCreated = DateTime.UtcNow,
                };
            User? newEntity = userService.CreateUser(newUser);
            Console.WriteLine("New user created.");

            return newEntity;
        }
    }
}
