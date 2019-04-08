using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class ProjecttypesController : BaseController<ProjectType>
    {
         public ProjecttypesController(
            IJsonApiContext jsonApiContext,
                IResourceService<ProjectType> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}