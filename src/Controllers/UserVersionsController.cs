using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserversionsController : BaseController<UserVersion>
    {
        public UserversionsController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
               IResourceService<UserVersion> resourceService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
         : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

        [AllowAnonymous]
        [HttpPost("{version}")]
        public IActionResult PostVersionAsync([FromRoute] string version)
        {
            return Ok( ((UserVersionService)service).StoreVersion(version));
        }

        [AllowAnonymous]
        [HttpPost("2/{version}")]
        public IActionResult PostVersionAsync([FromRoute] string version, [FromForm] string env)
        {
            return Ok(((UserVersionService)service).StoreVersion(version, env));
        }
    }
}

