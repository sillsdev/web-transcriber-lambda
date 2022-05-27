using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class PlantypesController : BaseController<Plantype>
    {
         public PlantypesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Plantype,int> resourceService,
            ICurrentUserContext currentUserContext,
  
            UserService userService)
          : base(loggerFactory, options, resourceGraph, resourceService, currentUserContext,  userService)
        { }
    }
}