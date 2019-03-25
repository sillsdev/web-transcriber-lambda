using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class ReviewersController : BaseController<Reviewer>
    {
         public ReviewersController(
            IJsonApiContext jsonApiContext,
                IResourceService<Reviewer> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}