using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;


namespace SIL.Transcriber.Controllers
{
    public class OrganizationsController : BaseController<Organization>
    {
         public OrganizationsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Organization> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] Organization entity)
        {
            //make sure SIL Auth knows about this org

            if (!organizationService.VerifyOrg(currentUserContext, entity))
            {
                throw new System.Exception("Organization does not exist in SIL Repository.");
            }

            if (entity.Owner == null)
            {
                entity.Owner = CurrentUser;
            }

            return await base.PostAsync(entity);
        }
    }

}