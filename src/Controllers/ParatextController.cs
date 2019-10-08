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
        private readonly IEntityRepository<UserSecret> _userSecrets;
        private readonly IParatextService _paratextService;

        public ParatextController(IEntityRepository<UserSecret> userSecrets, 
            IParatextService paratextService)
        {
            _userSecrets = userSecrets;
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
        public async Task<ActionResult<string>> UsernameAsync()
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
        public async Task<ActionResult<int>> PassageReadyToSyncCount([FromRoute] int planId)
        {
            int passages = await _paratextService.PlanPassagesToSyncCountAsync(planId);
            return Ok(passages);
        }

        [HttpPost("plan/{planid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostPlanAsync([FromRoute] int planId)
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            /* get all the sections that are ready to sync */
            
            List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(userSecret, planId);
            return Ok(chapters);
        }
        [HttpPost("project/{projectid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostProjectAsync([FromRoute] int projectId)
        {
            UserSecret userSecret = _paratextService.ParatextLogin();
            List<ParatextChapter> chapters = await _paratextService.SyncProjectAsync(userSecret, projectId);
            return Ok();
        }

    }
}