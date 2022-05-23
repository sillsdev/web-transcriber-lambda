using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserversionsController : ControllerBase
    {
        private readonly UserVersionService ResourceService;
        public UserversionsController(
           UserVersionService resourceService
        )
         : base()
        { ResourceService = resourceService; }

        [AllowAnonymous]
        [HttpPost("{version}")]
        public IActionResult PostVersionAsync([FromRoute] string version)
        {
            return Ok( ResourceService.StoreVersion(version));
        }

        [AllowAnonymous]
        [HttpPost("2/{version}")]
        public IActionResult PostVersionAsync([FromRoute] string version, [FromForm] string env)
        {
            return Ok(ResourceService.StoreVersion(version, env));
        }
    }
}

