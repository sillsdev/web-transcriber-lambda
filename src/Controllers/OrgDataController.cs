using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Controllers;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading.Tasks;

namespace TranscriberAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrgdatasController : BaseController<OrgData>
    {
        private readonly OrgDataService _service;
        public OrgdatasController(
             ILoggerFactory loggerFactory,
             IJsonApiContext jsonApiContext,
             IResourceService<OrgData> resourceService,
             ICurrentUserContext currentUserContext,
             OrganizationService organizationService,
             UserService userService, OrgDataService service)
            : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
            _service = service;
        }

        [HttpGet]
        public override async Task<IActionResult> GetAsync()
        {
            return Ok(await _service.GetAsync());
        }
    }
}