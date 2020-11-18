using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;


namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfflineProjectsController : BaseController<OfflineProject>
    {
        public OfflineProjectsController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
           IResourceService<OfflineProject> Service,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
        : base(loggerFactory, jsonApiContext, Service, currentUserContext, organizationService, userService)
        {
        }

    }
}