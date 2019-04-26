using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class BaseController<T> : BaseController<T, int> where T : class, IIdentifiable<int>
    {
        public BaseController(
            IJsonApiContext jsonApiContext,
            IResourceService<T, int> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService
            ) : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
        }
    }
    //REMOVE FOR NOW! [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BaseController<T, TId> : JsonApiController<T, TId> where T : class, IIdentifiable<TId>
    {
        protected IResourceService<T, TId> service;
        protected IJsonApiContext jsonApiContext;
        protected UserService userService;
        protected OrganizationService organizationService;
        protected ICurrentUserContext currentUserContext;

        public BaseController(
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
        }
    }
}
