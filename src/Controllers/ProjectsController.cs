using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;

namespace SIL.Transcriber.Controllers
{
    public class ProjectsController : BaseController<Project>
    {
         public ProjectsController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
                IResourceService<Project> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
        }
        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] Project entity)
        {
            if (entity.OwnerId == 0)
            {
                entity.OwnerId = CurrentUser.Id;
            }
            return await base.PostAsync(entity);
        }
    }
}