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
           FileResponseService frService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService,
           IOfflineDataService service)
        :base(loggerFactory, jsonApiContext, frService, currentUserContext, organizationService, userService)
        {
            _service = service;
        }

        [HttpGet("project/export/{id}/{start}")]
        public ActionResult<FileResponse> Export([FromRoute] int id, int start)
        {
            FileResponse response = _service.ExportProject(id, start);
            return Ok(response);
        }
        [HttpGet("project/import/{filename}")]
        public ActionResult<FileResponse> ImportFileUpload([FromRoute] string filename)
        {
            /* get a signed PUT url */
            FileResponse response = _service.ImportFileURL(filename);
            return response;
        }

        [HttpPut("project/import/{projectid}/{filename}")]
        public async Task<ActionResult<FileResponse>> ProcessImportFileAsync([FromRoute] int projectid, string filename)
        {
            return await _service.ImportFileAsync(projectid, filename);
        }

        [HttpPut("sync/{filename}")]
        public async Task<ActionResult<FileResponse>> ProcessSyncFileAsync([FromRoute] string filename)
        {
            return await _service.ImportFileAsync(filename);
        }

    }
}