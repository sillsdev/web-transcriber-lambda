using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using System.Threading.Tasks;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class UsersController : BaseController<User>
    {
        public UsersController(
           IJsonApiContext jsonApiContext,
               IResourceService<User> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
         : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
        /*
        [HttpGet("{id}/organizations")]
        public async Task<IActionResult> GetProjectToken(int id)
        {
            var project = await service.GetAsync(id);

        }
        */
    }
}
