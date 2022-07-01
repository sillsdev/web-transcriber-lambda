using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class PlansController : BaseController<Plan>
    {
        public PlansController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Plan, int> resourceService,
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

        [HttpPost]
        public override async Task<IActionResult> PostAsync(
            [FromBody] Plan entity,
            CancellationToken cancelled
        )
        {
            if ((entity.OwnerId ?? 0) == 0)
            {
                entity.OwnerId = CurrentUser?.Id;
            }
            return await base.PostAsync(entity, cancelled);
        }
    }
}
