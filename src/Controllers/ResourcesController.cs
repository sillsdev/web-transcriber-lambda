using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class ResourcesController : BaseController<Resource>
    {
        public ResourcesController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
           IResourceService<Resource> resourceService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
         : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}