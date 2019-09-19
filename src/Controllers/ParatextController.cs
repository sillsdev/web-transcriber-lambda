using System.Collections.Generic;
using System.Security;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ParatextController : ControllerBase
    {
        private readonly IEntityRepository<UserSecret> _userSecrets;
        private readonly IParatextService _paratextService;
        private readonly IUserAccessor _userAccessor;

        public ParatextController(IEntityRepository<UserSecret> userSecrets, IParatextService paratextService,
            IUserAccessor userAccessor)
        {
            _userSecrets = userSecrets;
            _paratextService = paratextService;
            _userAccessor = userAccessor;
        }


        [HttpGet("projects")]
        public async Task<ActionResult<IEnumerable<ParatextProject>>> GetAsync()
        {
            UserSecret userSecret = await _paratextService.ParatextLogin();

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
        { //TODO
            /*
            Attempt<UserSecret> attempt = await _userSecrets.TryGetAsync(_userAccessor.UserId);
            if (!attempt.TryResult(out UserSecret userSecret))
                return NoContent();
            string username = _paratextService.GetParatextUsername(userSecret);
            return Ok(username);
            */
            return Ok("sara_hentzel");
        }
        [AllowAnonymous] //temp for testing
        [HttpGet("section/{sectionid}")]
        public async Task<ActionResult<List<ParatextChapter>>> GetSectionBookAsync([FromRoute] int sectionId)
        {
            UserSecret userSecret = await _paratextService.ParatextLogin();
            List<ParatextChapter> chapters = await _paratextService.GetSectionChaptersAsync(userSecret, sectionId);
            return Ok(chapters);
        }
        [HttpPost("section/{sectionid}")]
        public async Task<ActionResult<List<ParatextChapter>>> PostSectionAsync([FromRoute] int sectionId)
        {
            UserSecret userSecret = await _paratextService.ParatextLogin();
            List<ParatextChapter> chapters = await _paratextService.SyncSectionAsync(userSecret, sectionId);
            return Ok(chapters);
        }
    }
}