using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class IntellectualpropertiesController : BaseController<Intellectualproperty>
    {
        public IntellectualpropertiesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Intellectualproperty, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        ) : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            )
        { }
    }
}
