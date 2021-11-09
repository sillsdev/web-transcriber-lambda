using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class OrgartifactcategorysController : BaseController<OrgArtifactCategory>
    {
        public OrgartifactcategorysController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<OrgArtifactCategory> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}