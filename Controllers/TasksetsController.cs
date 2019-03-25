using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class TasksetsController : BaseController<TaskSet>
    {
         public TasksetsController(
            IJsonApiContext jsonApiContext,
                IResourceService<TaskSet> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}