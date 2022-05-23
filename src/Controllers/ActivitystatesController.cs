using JsonApiDotNetCore.Configuration;
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
            IJsonApiOptions options,
            IResourceService<Activitystate> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService)
          : base(loggerFactory, options, resourceService, currentUserContext, userService)
        { }
    }
}