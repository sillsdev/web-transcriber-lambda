using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class WorkflowstepsController : BaseController<Workflowstep>
    {
        public WorkflowstepsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Workflowstep, int> resourceService,
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
