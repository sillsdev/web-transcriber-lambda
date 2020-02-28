using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    /* This has to be a BaseController of some sort to force JsonApiDotNetCore to serialize our objects so they are in right format in the zip file */

    [Route("api/[controller]")]
    [ApiController]
    public class OfflinedataController : BaseController<FileResponse>
    {
        private readonly IOfflineDataService _service;

        public OfflinedataController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
           IResourceService<FileResponse> frService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService,
           IOfflineDataService service)
        :base(loggerFactory, jsonApiContext, frService, currentUserContext, organizationService, userService)
        {
            _service = service;
        }

        [HttpGet("project/{id}")]
        public IActionResult Export([FromRoute] int id)
        {
            FileResponse response = _service.ExportProject(id);
            return Ok(response);
        }

        [HttpGet("project/import/{filename}")]
        public IActionResult ImportFileUpload([FromRoute] string filename)
        {
            /* get a signed PUT url */
            FileResponse response = _service.ImportFileURL(filename);
            return Ok(response);
        }

        [AllowAnonymous]
        [HttpPut("project/import/{filename}")]
        public async Task<IActionResult> ProcessImportFileAsync([FromRoute] string filename)
        {
            FileResponse response = await _service.ImportFileAsync(filename);
            if (response.Status == System.Net.HttpStatusCode.OK)
                return Ok(response);
            return BadRequest(response);
        }




    }
}