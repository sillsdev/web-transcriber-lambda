using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class SectionresourcesController : BaseController<Sectionresource>
    {
        public SectionresourcesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Sectionresource, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService)
          : base(loggerFactory, options, resourceGraph, resourceService, currentUserContext, userService)
        { }
    }
}