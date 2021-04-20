using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
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