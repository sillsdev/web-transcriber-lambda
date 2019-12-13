using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class PlansController : BaseController<Plan>
    {
        public PlansController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<Plan> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
         : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] Plan entity)
        {
            if (entity.OwnerId == 0)
            {
                entity.OwnerId = CurrentUser.Id;
            }
            return await base.PostAsync(entity);
        }
    }
}