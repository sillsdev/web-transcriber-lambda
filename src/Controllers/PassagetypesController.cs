using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class PassagetypesController : BaseController<Passagetype>
    {
        public PassagetypesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Passagetype, int> resourceService,
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
