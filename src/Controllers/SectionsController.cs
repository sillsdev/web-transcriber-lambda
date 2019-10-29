using System.Threading.Tasks;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class SectionsController : BaseController<Section>
    {
        public SectionsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
           IResourceService<Section> resourceService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService)
         : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
            SectionService = (SectionService)resourceService;
        }
        SectionService SectionService;
        [HttpPost("{Id}/{role}/{userId}")]
        public async Task<IActionResult> AssignAsync([FromRoute] int Id, [FromRoute] int userId, [FromRoute] string role)
        {
            if (await SectionService.GetAsync(Id) == null)
                return NotFound();

            return Ok(((SectionService)service).AssignUser(Id, userId, role));    
        }
        [HttpGet("{Id}/assignments")]
        public async Task<IActionResult> GetAssignAsync([FromRoute] int Id)
        {
            if (await SectionService.GetAsync(Id) == null)
                return NotFound();
            return Ok(((SectionService)service).GetAssignedUsers(Id));
        }
        [HttpDelete("{Id}/{role}")]
        public async Task<IActionResult> DeleteAssignment([FromRoute] int Id, [FromRoute] string role)
        {
            if (await SectionService.GetAsync(Id) == null)
                return NotFound();

            return Ok(((SectionService)service).DeleteAssignment(Id, role));
        }
        [HttpGet("project/{id}/status/{status}")]
        public IActionResult GetSectionsWithStatus([FromRoute] int id, [FromRoute] string status)
        {
            var sections = SectionService.GetSectionsAtStatus(id, status);
            return Ok(sections);
        }
    }
}