﻿using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Services;
using SIL.Transcriber.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using SIL.Auth.Models;
using System;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class BaseController<T> : BaseController<T, int> where T : class, IIdentifiable<int>
    {
        public BaseController(
            ILoggerFactory loggerFactory, 
            IJsonApiContext jsonApiContext,
            IResourceService<T, int> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService
            ) : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
        }
    }
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BaseController<T, TId> : JsonApiController<T, TId> where T : class, IIdentifiable<TId>
    {
        protected IResourceService<T, TId> service;
        protected IJsonApiContext jsonApiContext;
        protected UserService userService;
        protected OrganizationService organizationService;
        protected ICurrentUserContext currentUserContext;
        protected ILogger<T> Logger { get; set; }

        public BaseController(
            ILoggerFactory loggerFactory, 
            IJsonApiContext jsonApiContext,
            IResourceService<T, TId> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService
            ) : base(jsonApiContext, resourceService)
        {
            this.service = resourceService;
            this.jsonApiContext = jsonApiContext;
            this.userService = userService;
            this.organizationService = organizationService;
            this.currentUserContext = currentUserContext;
            this.Logger = loggerFactory.CreateLogger<T>();
        }
        private static string CURRENT_USER_KEY = "CurrentUser";

        /*  Nice try...but the errors don't come back to here...have to interrupt the jsonapi error handling
        public override async Task<IActionResult> PostAsync([FromBody] T entity)
        {
            try
            {
                return await base.PostAsync(entity);

            }
            catch (DbException ex)
            {

                return BadRequest(ex);
            }
        }
        */
        public User CurrentUser
        {
            get
            {
                var exists = HttpContext.Items.ContainsKey(CURRENT_USER_KEY);
                var existing = HttpContext.Items[CURRENT_USER_KEY];

                if (exists && existing != null) return (User)existing;

                // current user has not yet been found for this request.
                // find or create because users are managed by auth0 and
                // creation isn't proxied through the api.
                    var user = FindOrCreateCurrentUser().Result;
                    HttpContext.Items[CURRENT_USER_KEY] = user;
                    return user;
            }
        }
       
        private async Task<User> FindOrCreateCurrentUser()
        {
            var existing = await userService.GetCurrentUser();

            if (existing != null) return existing;

            var newUser = new User
            {
                ExternalId = currentUserContext.Auth0Id,
                Email = currentUserContext.Email,
                Name = currentUserContext.Name,
                GivenName = currentUserContext.GivenName,
                FamilyName = currentUserContext.FamilyName,
                SilUserid = currentUserContext.SilUserid
            };

            var newEntity = await userService.CreateAsync(newUser);
            Console.WriteLine("New user created.");
            /* ask the sil auth if this user has any orgs */
            List<SILAuth_Organization> orgs = currentUserContext.SILOrganizations;
            Console.WriteLine("orgs", orgs.Count);
            organizationService.JoinOrgs(orgs, newEntity, RoleName.Member);
           
            return newEntity;
        }
    }
}
