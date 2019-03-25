using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class OrganizationmembershipsController : BaseController<OrganizationMembership>
    {
         public OrganizationmembershipsController(
            IJsonApiContext jsonApiContext,
                IResourceService<OrganizationMembership> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}