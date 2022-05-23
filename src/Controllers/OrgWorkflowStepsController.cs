using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class OrgworkflowstepsController : BaseController<OrgWorkflowstep>
    {
        public OrgworkflowstepsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<OrgWorkflowstep,int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService)
          : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext, userService)
        { }
    }
}