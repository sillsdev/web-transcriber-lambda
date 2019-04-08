using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class UsertasksController : BaseController<UserTask>
    {
         public UsertasksController(
            IJsonApiContext jsonApiContext,
                IResourceService<UserTask> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}