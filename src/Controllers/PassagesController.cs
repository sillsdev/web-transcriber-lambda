using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class PassagesController : BaseController<Passage>
    {
        public PassagesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Passage, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        )
            : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            ) { }
    }
}
