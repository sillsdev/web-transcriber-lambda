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
    public class ProjdatasController : BaseController<ProjData>
    {
        public ProjdatasController(
             ILoggerFactory loggerFactory,
             IJsonApiContext jsonApiContext,
             IResourceService<ProjData> resourceService,
             ICurrentUserContext currentUserContext,
             OrganizationService organizationService,
             UserService userService)
            : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
        }
    }
}