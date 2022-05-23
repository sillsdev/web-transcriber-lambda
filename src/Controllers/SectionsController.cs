using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class SectionsController : BaseController<Section>
    {

        public SectionsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
           IResourceGraph resourceGraph,
           IResourceService<Section,int> resourceService,
           ICurrentUserContext currentUserContext,
           UserService userService)
         : base(loggerFactory, options, resourceGraph, resourceService, currentUserContext,  userService)
        {
        }
    }
}