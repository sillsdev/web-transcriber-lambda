
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Controllers;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
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
    }
}