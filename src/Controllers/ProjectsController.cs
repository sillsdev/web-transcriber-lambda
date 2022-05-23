using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using JsonApiDotNetCore.Configuration;
using System.Threading;

namespace SIL.Transcriber.Controllers
{
    public class ProjectsController : BaseController<Project>
    {
         public ProjectsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Project,int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService)
          : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext,  userService)
        {
        }
        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] Project entity, CancellationToken cancelled)
        {
            if (entity.OwnerId == 0)
            {
                entity.OwnerId = CurrentUser?.Id;
            }
            return await base.PostAsync(entity,cancelled);
        }
    }
}