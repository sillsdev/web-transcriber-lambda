using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class ProjectsController : BaseController<Project>
    {
         public ProjectsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Project> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}