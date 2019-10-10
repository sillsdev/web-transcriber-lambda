using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class PassagesectionsController : BaseController<PassageSection>
    {
         public PassagesectionsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
                IResourceService<PassageSection> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

    }
}