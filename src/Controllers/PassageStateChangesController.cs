using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using System.Threading;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Serialization.Objects;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PassagestatechangesController : BaseController<PassageStateChange>
    {
        public PassagestatechangesController(
           ILoggerFactory loggerFactory,
           IJsonApiOptions options,
           IResourceGraph resourceGraph,
           IResourceService<PassageStateChange, int> resourceService,
           ICurrentUserContext currentUserContext,
 
           UserService userService)
        : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext,  userService)
        {
        }

#pragma warning disable 1998
        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(int id, CancellationToken cancelled)
        {
            throw new JsonApiException(new ErrorObject(System.Net.HttpStatusCode.NotImplemented));
        }
#pragma warning restore 1998
    }
}