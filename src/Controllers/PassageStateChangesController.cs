using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PassagestatechangesController : BaseController<PassageStateChange>
    {
        public PassagestatechangesController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
           IResourceService<PassageStateChange> myService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
        : base(loggerFactory, jsonApiContext, myService, currentUserContext, organizationService, userService)
        {
        }

#pragma warning disable 1998
        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(int id)
        {
            throw new JsonApiException(405, $"Not implemented for State Change.");
        }
#pragma warning restore 1998
    }
}