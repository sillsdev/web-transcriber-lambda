using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class TaskstatesController : BaseController<TaskState>
    {
         public TaskstatesController(
            IJsonApiContext jsonApiContext,
                IResourceService<TaskState> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}