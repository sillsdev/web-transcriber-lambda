using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class PlansController : BaseController<Plan>
    {
        public PlansController(
           IJsonApiContext jsonApiContext,
            IResourceService<Plan> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
         : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] Plan entity)
        {
            if (entity.ProjectId == 0)
            {
                //save the project
                if (entity.Project != null)
                {
                    if (entity.Project.Id > 0)
                        entity.ProjectId = entity.Project.Id;
                    else
                    {
                        //save it;
                    }
                };
            }
            return await base.PostAsync(entity);
        }
    }
}