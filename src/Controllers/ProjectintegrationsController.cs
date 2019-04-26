using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class ProjectintegrationsController : BaseController<ProjectIntegration>
    {
         public ProjectintegrationsController(
            IJsonApiContext jsonApiContext,
                IResourceService<ProjectIntegration> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}