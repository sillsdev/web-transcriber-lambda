using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class OrgworkflowstepsController : BaseController<OrgWorkflowStep>
    {
        public OrgworkflowstepsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<OrgWorkflowStep> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}