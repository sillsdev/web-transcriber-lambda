using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class TaskmediaController : BaseController<TaskMedia>
    {
         public TaskmediaController(
            IJsonApiContext jsonApiContext,
                IResourceService<TaskMedia> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}