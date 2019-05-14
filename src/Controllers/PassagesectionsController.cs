using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class PassagesectionsController : BaseController<PassageSection>
    {
         public PassagesectionsController(
            IJsonApiContext jsonApiContext,
                IResourceService<PassageSection> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

        [HttpPost]
        public override async System.Threading.Tasks.Task<IActionResult> PostAsync([FromBody] PassageSection entity)
        {
            if (entity.Section.Id == 0)
            {
                //save the section
                ;
            }
            if (entity.Passage.Id == 0)
            {
                //save the passage
            }

            return await base.PostAsync(entity);
        }
    }
}