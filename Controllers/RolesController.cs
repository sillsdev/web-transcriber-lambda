using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class RolesController : BaseController<Role>
    {
         public RolesController(
            IJsonApiContext jsonApiContext,
                IResourceService<Role> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}