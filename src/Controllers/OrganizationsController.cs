using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class OrganizationsController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Organization, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Organization>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {
        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync(
            [FromBody] Organization entity,
            CancellationToken cancelled
        )
        {
            entity.Owner ??= CurrentUser;
            return await base.PostAsync(entity, cancelled);
        }
    }
}
