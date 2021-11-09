using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class WorkflowstepsController : BaseController<WorkflowStep>
    {
        public WorkflowstepsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<WorkflowStep> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}