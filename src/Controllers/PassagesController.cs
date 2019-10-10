using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class PassagesController : BaseController<Passage>
    {
         public PassagesController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
                IResourceService<Passage> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

    }
}

