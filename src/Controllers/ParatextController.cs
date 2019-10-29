using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Paratext.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/[controller]")]
    [ApiController]
    public class ParatextController : ControllerBase
    {
        private readonly IParatextService _paratextService;

        public ParatextController(IParatextService paratextService)
        {
            _paratextService = paratextService;
        }


        [HttpGet("projects")]
        public async Task<ActionResult<IEnumerable<ParatextProject>>> GetAsync()
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            try
            {
                IReadOnlyList<ParatextProject> projects = await _paratextService.GetProjectsAsync(userSecret);
                return Ok(projects);
            }
            catch (SecurityException)
            {
                return NoContent();
            }
        }

        [HttpGet("username")]
        public ActionResult<string> UsernameAsync()
        {
            UserSecret userSecret = _paratextService.ParatextLogin();

            string username = _paratextService.GetParatextUsername(userSecret);
            return Ok(username);
        }

        [HttpGet("section/{sectionid}")]
        public async Task<ActionResult<List<ParatextChapter>>> GetSectionBookAsync([FromRoute] int sectionId)
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            List<ParatextChapter> chapters = await _paratextService.GetSectionChaptersAsync(userSecret, sectionId);
            return Ok(chapters);
        }
        [HttpGet("project/{projectId}/count")]
        public async Task<ActionResult<int>> ProjectPassagesToSyncCount([FromRoute] int projectId)
        {

            int passages = await _paratextService.ProjectPassagesToSyncCountAsync(projectId);
            return Ok(passages);
        }

        [HttpGet("plan/{planid}/count")]
        public ActionResult<int> PassageReadyToSyncCount([FromRoute] int planId)
        {
            int passages = _paratextService.PlanPassagesToSyncCount(planId);
            return Ok(passages);
        }

        [HttpPost("plan/{planid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostPlanAsync([FromRoute] int planId)
        {
            /* get all the sections that are ready to sync */
            List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(_paratextService.ParatextLogin(), planId);
            return Ok(chapters);
        }
        [HttpPost("project/{projectid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostProjectAsync([FromRoute] int projectId)
        {
            List<ParatextChapter> chapters = await _paratextService.SyncProjectAsync(_paratextService.ParatextLogin(), projectId);
            return Ok();
        }

    }
}