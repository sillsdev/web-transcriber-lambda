using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class ProjectintegrationsController : BaseController<ProjectIntegration>
    {
         public ProjectintegrationsController(
            IJsonApiContext jsonApiContext,
                IResourceService<ProjectIntegration> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}