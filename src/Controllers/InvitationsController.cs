using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;
using System.Net.Http;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class InvitationsController : BaseController<Invitation>
    {
        public InvitationsController(
          IJsonApiContext jsonApiContext,
           IResourceService<Invitation> invitationService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
        : base(jsonApiContext, invitationService, currentUserContext, organizationService, userService)
        {
        }

    }
}