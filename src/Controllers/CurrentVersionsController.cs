using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //[NoHttpDelete]
    //[NoHttpPatch]
    [Route("api/[controller]")]
    [ApiController]
    public class CurrentversionsController : BaseController<Currentversion>
    {
        public CurrentversionsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Currentversion, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        ) : base(
               loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            )
        { }

        [AllowAnonymous]
        [HttpPost("{version}")]
        public IActionResult PostVersionAsync([FromRoute] string version)
        {
            return Ok(((CurrentversionService)service).StoreVersion(version));
        }
    }
}
