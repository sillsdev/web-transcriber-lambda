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
    public class ParatextController(IParatextService paratextService, ILoggerFactory loggerFactory) : ControllerBase
    {
        private readonly IParatextService _paratextService = paratextService;
        protected ILogger<ParatextController> Logger { get; set; } = loggerFactory.CreateLogger<ParatextController>();

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
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "Projects error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
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
        [HttpGet("canpublish")]
        public async Task<ActionResult<bool>> CanPublish()
        {
            UserSecret userSecret;
            try
            {
                userSecret = _paratextService.ParatextLogin();
            }
            catch
            {
                return Ok(false);
            }

            bool canpublish = await _paratextService.GetCanPublishAsync(userSecret);
            return Ok(canpublish);
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
        [HttpGet("passage/{passageid}/{type}/count")]
        public ActionResult<int> PassageToSyncCount([FromRoute] int passageid,
                                                                [FromRoute] int type)
        {
            int passages = _paratextService.PassageToSyncCount(passageid, type);
            return Ok(passages);
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
            try
            {
                /* get all the sections that are ready to sync */
                List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(
                userSecret,
                planId,
                0
            );
                return Ok();
            }
            catch (Exception ex)
            {
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "SyncPlanAsync error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
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
            try
            {
                /* get all the sections that are ready to sync */
                List<ParatextChapter> chapters = await _paratextService.SyncPlanAsync(
                userSecret,
                planId,
                type);
                return Ok();
            }
            catch (Exception ex)
            {
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "SyncPlanAsync error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
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
            try
            {
                _ = await _paratextService.SyncProjectAsync(
                    userSecret,
                    projectId,
                    0
                );
                return Ok();
            }
            catch (Exception ex)
            {
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "SyncProjectAsync error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
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
            try
            {
                _ = await _paratextService.SyncProjectAsync(
                    userSecret,
                    projectId,
                    type
                );
                return Ok();
            }
            catch (Exception ex)
            {
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "SyncProjectAsync error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
        }
        [HttpPost("passage/{passageid}/{type}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostPassageAsync([FromRoute] int passageId,
                                                                                [FromRoute] int type)
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

                List<ParatextChapter> chapters = await _paratextService.SyncPassageAsync(
                userSecret,
                passageId,
                type);
                return Ok();
            }
            catch (Exception ex)
            {
                return new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "SyncPassageAsync error",
                    Detail = ex.Message,
                    Instance = HttpContext.Request.Path
                });
            }
        }
    }
}
