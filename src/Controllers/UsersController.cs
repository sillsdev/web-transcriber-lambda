using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class UsersController : BaseController<User>
    {
         public UsersController(
            IJsonApiContext jsonApiContext,
                IResourceService<User> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}