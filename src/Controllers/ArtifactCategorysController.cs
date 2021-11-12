using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class ArtifactcategorysController : BaseController<ArtifactCategory>
    {
        public ArtifactcategorysController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<ArtifactCategory> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}