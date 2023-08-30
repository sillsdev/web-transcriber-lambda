using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    public class AmIOnlineController : Controller
    {
        [AllowAnonymous]
        [HttpGet()]
        public IActionResult Get()
        {
            return Ok(true);
        }
    }
}
