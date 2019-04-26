using JsonApiDotNetCore.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class GroupsController : BaseController<Group>
    {
        public IOrganizationContext OrganizationContext { get; set; }
        public GroupsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Group> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
        /*
        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] Group entity)
        {
            if (OrganizationContext.SpecifiedOrganizationDoesNotExist) return StatusCode(StatusCodes.Status404NotFound);
            if (!OrganizationContext.HasOrganization) return StatusCode(StatusCodes.Status422UnprocessableEntity);

            entity.OwnerId = OrganizationContext.OrganizationId;

            return await base.PostAsync(entity);
        }
        */
    }

}