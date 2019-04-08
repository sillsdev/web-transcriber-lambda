using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class IntegrationsController : BaseController<Integration>
    {
         public IntegrationsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Integration> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}