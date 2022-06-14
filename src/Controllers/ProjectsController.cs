using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.AspNetCore.Mvc;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Controllers
{
    public class ProjectsController : BaseController<Project>
    {
        public ProjectsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Project, int> resourceService,
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
    }
}
