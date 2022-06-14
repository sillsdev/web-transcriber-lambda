using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class ArtifactcategoriesController : BaseController<Artifactcategory>
    {
        public ArtifactcategoriesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Artifactcategory, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        )
            : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            ) { }
    }
}
