using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class SectionsController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Section, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Section>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {
        private readonly SectionService service = (SectionService)resourceService;

        [HttpPatch("assign/{scheme}/{idlist}")]
        public IActionResult AssignSections([FromRoute] int scheme, [FromRoute] string idlist)
        {
            try
            {
                return Ok(service.AssignSections(scheme, idlist));
            }
            catch
            {
                return NotFound();
            }
        }
    }
}
