using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    public class UsersController : BaseController<User>
    {
        public UsersController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<User,int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService)
         : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext,  userService)
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
        public async Task<IActionResult?> GetCurrentUser()
        {
            User? currentUser = CurrentUser;
            if (currentUser == null) return null;
            return await base.GetAsync(currentUser.Id, new System.Threading.CancellationToken());
        }

    }
}
