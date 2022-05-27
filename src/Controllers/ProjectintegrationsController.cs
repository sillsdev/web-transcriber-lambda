using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class ProjectintegrationsController : BaseController<Projectintegration>
    {
         public ProjectintegrationsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Projectintegration,int> resourceService,
            ICurrentUserContext currentUserContext,
  
            UserService userService)
          : base(loggerFactory, options, resourceGraph, resourceService, currentUserContext,  userService)
        { }
    }
}