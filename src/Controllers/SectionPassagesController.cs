using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SIL.Transcriber.Controllers;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading.Tasks;

namespace TranscriberAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SectionpassagesController : BaseController<SectionPassage>
    {
        private readonly SectionPassagesService _service;
        public SectionpassagesController(
             ILoggerFactory loggerFactory,
             IJsonApiContext jsonApiContext,
             IResourceService<SectionPassage> resourceService,
             ICurrentUserContext currentUserContext,
             OrganizationService organizationService,
             UserService userService,
             SectionPassagesService service)
            : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
            _service = service;
        }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] SectionPassage entity)
        {
            return Ok(_service.PostAsync(entity));
        }

    }
}