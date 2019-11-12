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
        SectionService SectionService;

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
        [HttpGet("project/{id}/status/{status}")]
        public IActionResult GetSectionsWithStatus([FromRoute] int id, [FromRoute] string status)
        {
            var sections = SectionService.GetSectionsAtStatus(id, status);
            return Ok(sections);
        }
    }
}