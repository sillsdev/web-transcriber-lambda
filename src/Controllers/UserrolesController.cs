using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class UserrolesController : BaseController<UserRole>
    {
         public UserrolesController(
            IJsonApiContext jsonApiContext,
                IResourceService<UserRole> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}