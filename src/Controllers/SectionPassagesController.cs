using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Controllers;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading;
using System.Threading.Tasks;


namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SectionpassagesController : BaseController<Sectionpassage>
    {
        private readonly SectionPassageService _service;

        public SectionpassagesController(
             ILoggerFactory loggerFactory,
             IJsonApiOptions options,
             IResourceGraph resourceGraph,
             IResourceService<Sectionpassage,int> resourceService,
             ICurrentUserContext currentUserContext,
             UserService userService,
             SectionPassageService service)
            : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext,  userService)
        {
            _service = service;
        }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] Sectionpassage entity, CancellationToken cancelled)
        {
            return Ok(await _service.PostAsync(entity));

        }
    }
}