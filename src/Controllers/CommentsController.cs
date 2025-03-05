using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class CommentsController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Comment, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Comment>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {
    }
}
