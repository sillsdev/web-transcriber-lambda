using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //[NoHttpDelete]
    //[NoHttpPatch]
    [Route("api/[controller]")]
    [ApiController]
    public class CurrentversionsController : BaseController<CurrentVersion>
    {
        public CurrentversionsController(
           ILoggerFactory loggerFactory,
           IJsonApiOptions options,
           IResourceGraph resourceGraph, IResourceService<CurrentVersion,int> resourceService,
           ICurrentUserContext currentUserContext,
           UserService userService)
         : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext, userService)
        { }

        [AllowAnonymous]
        [HttpPost("{version}")]
        public IActionResult PostVersionAsync([FromRoute] string version)
        {
            return Ok( ((CurrentVersionService)service).StoreVersion(version));
        }
    }
}

