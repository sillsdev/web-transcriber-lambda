using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class OrganizationmembershipsController : BaseController<OrganizationMembership>
    {
         public OrganizationmembershipsController(
            IJsonApiContext jsonApiContext,
                IResourceService<OrganizationMembership> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}