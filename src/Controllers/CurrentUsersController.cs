using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    public class CurrentusersController : BaseController<User>
    {
        public CurrentusersController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
               IResourceService<User> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
         : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
        #pragma warning disable 1998
        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] User entity)
        {
            throw new JsonApiException(405, $"Not implemented for Current User resource.");
        }

        [HttpGet]
        public override async Task<IActionResult> GetAsync()
        {
            var currentUser = CurrentUser;

            return Ok(currentUser);
        }
        #pragma warning restore 1998
    }
}