using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    public class ProjectsController : BaseController<Project>
    {
         public ProjectsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Project> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}