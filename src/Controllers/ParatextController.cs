using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Paratext.Models;
using SIL.Transcriber.Services;
using System.Security;

namespace SIL.Transcriber.Controllers
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [Route("api/[controller]")]
    [ApiController]
    public class ParatextController : ControllerBase
    {
        private readonly IParatextService _paratextService;
        protected ILogger<ParatextController> Logger { get; set; }

        public ParatextController(IParatextService paratextService, ILoggerFactory loggerFactory)
        {
            _paratextService = paratextService;
            this.Logger = loggerFactory.CreateLogger<ParatextController>();
        }

        [HttpGet("orgs")]
        public async Task<ActionResult<IEnumerable<ParatextOrg>>> GetOrgsAsync()
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }
            try
            {
                IReadOnlyList<ParatextOrg> orgs = await _paratextService.GetOrgsAsync(userSecret);
                return Ok(orgs);
            }
            catch (SecurityException)
            {
                return NoContent();
            }
        }

        [HttpGet("projects")]
#pragma warning disable IDE0060 // Remove unused parameter
        public async Task<ActionResult<IEnumerable<ParatextProject>>> GetAsync(
            CancellationToken cancellationToken
        )
#pragma warning restore IDE0060 // Remove unused parameter
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }
            try
            {
                IReadOnlyList<ParatextProject>? projects = await _paratextService.GetProjectsAsync(
                    userSecret
                );
                return Ok(projects);
            }
            catch (SecurityException)
            {
                return NoContent();
            }
        }

        [HttpGet("projects/{languagetag}")]
        public async Task<ActionResult<IEnumerable<ParatextProject>>> GetAsync(
            [FromRoute] string languageTag
        )
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }
            try
            {
                IReadOnlyList<ParatextProject>? projects = await _paratextService.GetProjectsAsync(
                    userSecret,
                    languageTag
                );
                return Ok(projects);
            }
            catch (Exception ex)
            {
                Logger.LogError("Paratext Error projects get {message} {lang} {token}",
                                ex.Message,
                                languageTag,
                                userSecret.ParatextTokens.IssuedAt.ToString());
                throw;
                //return NoContent();
            }
        }

        [HttpGet("username")]
        public ActionResult<string?> Username()
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }

            string? username = _paratextService.GetParatextUsername(userSecret);
            return Ok(username);
        }

        [HttpGet("useremail/{inviteId}")]
        public ActionResult<string> UserEmails([FromRoute] string inviteId)
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }

            Utility.Attempt<string?> x = _paratextService
                .TryGetUserEmailsAsync(userSecret, inviteId)
                .Result;
            return Ok(x.Result);
        }

        [HttpGet("section/{sectionid}")]
        public async Task<ActionResult<List<ParatextChapter>>> GetSectionBookAsync(
            [FromRoute] int sectionId
        )
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }
            List<ParatextChapter> chapters = await _paratextService.GetSectionChaptersAsync(
                userSecret,
                sectionId,
                0
            );
            return Ok(chapters);
        }

        [HttpGet("project/{projectId}/count")]
        public async Task<ActionResult<int>> ProjectPassagesToSyncCount([FromRoute] int projectId)
        {
            int passages = await _paratextService.ProjectPassagesToSyncCountAsync(projectId, 0);
            return Ok(passages);
        }

        [HttpGet("project/{projectId}/{type}/count")]
        public async Task<ActionResult<int>> ProjectPassagesToSyncCount(
            [FromRoute] int projectId,
            [FromRoute] int type
        )
        {
            int passages = await _paratextService.ProjectPassagesToSyncCountAsync(projectId, type);
            return Ok(passages);
        }

        [HttpGet("plan/{planid}/count")]
        public ActionResult<int> PassageReadyToSyncCount([FromRoute] int planId)
        {
            int passages = _paratextService.PlanPassagesToSyncCount(planId, 0); //vernacular
            return Ok(passages);
        }

        [HttpGet("plan/{planid}/{type}/count")]
        public ActionResult<int> PassageReadyToSyncCount(
            [FromRoute] int planId,
            [FromRoute] int type
        )
        {
            int passages = _paratextService.PlanPassagesToSyncCount(planId, type);
            return Ok(passages);
        }

        [HttpGet("passage/{passageid}")]
        public async Task<ActionResult<string>> PassageTextAsync([FromRoute] int passageid)
        {
            string text = await _paratextService.PassageTextAsync(passageid, 0) ?? "";
            return Ok(text);
        }

        [HttpGet("passage/{passageid}/{type}")]
        public async Task<ActionResult<string>> PassageTextAsync(
            [FromRoute] int passageid,
            [FromRoute] int type
        )
        {
            string text = await _paratextService.PassageTextAsync(passageid, type) ?? "";
            return Ok(text);
        }

        [HttpPost("plan/{planid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostPlanAsync([FromRoute] int planId)
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }
            /* get all the sections that are ready to sync */
            List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(
                userSecret,
                planId,
                0
            );
            return Ok(chapters);
        }

        [HttpPost("plan/{planid}/{type}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostPlanAsync(
            [FromRoute] int planId,
            [FromRoute] int type
        )
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }
            /* get all the sections that are ready to sync */
            List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(
                userSecret,
                planId,
                type
            );
            return Ok(chapters);
        }

        [HttpPost("project/{projectid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostProjectAsync(
            [FromRoute] int projectId
        )
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }

            _ = await _paratextService.SyncProjectAsync(
                userSecret,
                projectId,
                0
            );
            return Ok();
        }

        [HttpPost("project/{projectid}/{type}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostProjectAsync(
            [FromRoute] int projectId,
            [FromRoute] int type
        )
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch (Exception e)
            {
                return ValidationProblem(new ValidationProblemDetails { Detail = e.Message });
            }

            _ = await _paratextService.SyncProjectAsync(
                userSecret,
                projectId,
                type
            );
            return Ok();
        }
    }
}
