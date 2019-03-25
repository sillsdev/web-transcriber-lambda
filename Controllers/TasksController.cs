using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class TasksController : BaseController<Task>
    {
         public TasksController(
            IJsonApiContext jsonApiContext,
                IResourceService<Task> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}