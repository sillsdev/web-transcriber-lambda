using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class OrganizationsController : BaseController<Organization>
    {
         public OrganizationsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Organization> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }

}