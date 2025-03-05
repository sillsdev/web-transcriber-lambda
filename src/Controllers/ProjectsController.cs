using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class ProjectsController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Project, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Project>(
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
