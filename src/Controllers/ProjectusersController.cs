using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class ProjectusersController : BaseController<ProjectUser>
    {
         public ProjectusersController(
            IJsonApiContext jsonApiContext,
                IResourceService<ProjectUser> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}