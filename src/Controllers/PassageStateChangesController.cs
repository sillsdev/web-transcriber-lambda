using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    public class PassagestatechangesController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Passagestatechange, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Passagestatechange>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {

#pragma warning disable 1998
        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(int id, CancellationToken cancelled)
        {
            throw new JsonApiException(new ErrorObject(System.Net.HttpStatusCode.NotImplemented));
        }
#pragma warning restore 1998
    }
}
