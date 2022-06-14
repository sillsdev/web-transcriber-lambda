using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class OrganizationsController : BaseController<Organization>
    {
        public OrganizationsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Organization, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        )
            : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            ) { }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync(
            [FromBody] Organization entity,
            CancellationToken cancelled
        )
        {
            if (entity.Owner == null)
            {
                entity.Owner = CurrentUser;
            }
            return await base.PostAsync(entity, cancelled);
        }
    }
}
