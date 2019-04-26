using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;


namespace SIL.Transcriber.Controllers
{
    public class IntegrationsController : BaseController<Integration>
    {
         public IntegrationsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Integration> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}