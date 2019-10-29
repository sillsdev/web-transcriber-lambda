using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvitationsController : BaseController<Invitation>
    {
        public InvitationsController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
           IResourceService<Invitation> invitationService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
        : base(loggerFactory, jsonApiContext, invitationService, currentUserContext, organizationService, userService)
        {
        }

    }
}