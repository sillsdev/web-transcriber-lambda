using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class UsersController : BaseController<User>
    {
        public UsersController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<User, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        ) : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            )
        { }

        /*  this makes the api not work...
        [HttpPost]
        public Task<IActionResult> PostAsync([FromBody] User entity)
        {
            throw new Exception($"Not implemented for User resource.");
        }
        */
        [HttpGet("current-user")]
        public async Task<IActionResult?> GetCurrentUser()
        {
            User? currentUser = CurrentUser;
            return currentUser != null ? await base.GetAsync(currentUser.Id, new System.Threading.CancellationToken()) : null;
        }
    }
}
