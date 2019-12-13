using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class ProjectintegrationsController : BaseController<ProjectIntegration>
    {
         public ProjectintegrationsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<ProjectIntegration> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}