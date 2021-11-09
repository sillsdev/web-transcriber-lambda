using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class ArtifacttypesController : BaseController<ArtifactType>
    {
        public ArtifacttypesController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<ArtifactType> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}