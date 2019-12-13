using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class ActivitystatesController : BaseController<Activitystate>
    {
         public ActivitystatesController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
                IResourceService<Activitystate> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}