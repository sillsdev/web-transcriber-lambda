using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using System.Threading.Tasks;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Internal;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class UsersController : BaseController<User>
    {
        public UsersController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
               IResourceService<User> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
         : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
#if false
#pragma warning disable 1998
        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] User entity)
        {
            throw new JsonApiException(405, $"Not implemented for User resource.");
        }
#pragma warning restore 1998
#endif

        [HttpGet("current-user")]
        public async Task<IActionResult> GetCurrentUser()
        {
            User currentUser = CurrentUser;

            return await base.GetAsync(currentUser.Id);
        }

    }
}
