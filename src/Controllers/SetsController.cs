using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class SetsController : BaseController<Set>
    {
         public SetsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Set> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}