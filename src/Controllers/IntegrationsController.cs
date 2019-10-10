using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;


namespace SIL.Transcriber.Controllers
{
    public class IntegrationsController : BaseController<Integration>
    {
         public IntegrationsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
                IResourceService<Integration> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}