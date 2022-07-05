using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    /* This has to be a BaseController of some sort to force JsonApiDotNetCore to serialize our objects so they are in right format in the zip file */

    [ApiController]
    [Route("api/offlineData")]
    public class OfflinedataController : ControllerBase
    {
        private readonly IOfflineDataService _service;

        public OfflinedataController(
            //ILoggerFactory loggerFactory,
            //IJsonApiOptions options,
            //IResourceGraph resourceGraph,
            //JsonApiResourceService<Fileresponse, int> frService,
            //ICurrentUserContext currentUserContext,
            //UserService userService,
            IOfflineDataService service
        ) : base()
        {
            _service = service;
        }

        [HttpGet("project/export/{id}/{start}")]
        public ActionResult<Fileresponse> Export([FromRoute] int id, int start)
        {
            Fileresponse response = _service.ExportProjectPTF(id, start);
            return Ok(response);
        }

        [HttpPost("project/export/{exporttype}/{id}/{start}")]
        public ActionResult<Fileresponse> Export(
            [FromRoute] string exportType,
            int id,
            int start,
            [FromForm] string? ids,
            [FromForm] string? artifactType
        )
        {
            Fileresponse response = exportType switch
            {
                "ptf" => _service.ExportProjectPTF(id, start),
                "audio" => _service.ExportProjectAudio(id, artifactType ?? "", ids, start),
                "burrito" => _service.ExportBurrito(id, ids, start),
                _ => _service.ExportProjectPTF(id, start),
            };
            return Ok(response);
        }

        [HttpGet("project/import/{filename}")]
        public ActionResult<Fileresponse> ImportFileUpload([FromRoute] string filename)
        {
            /* get a signed PUT url */
            Fileresponse response = _service.ImportFileURL(filename);
            return response;
        }

        [HttpPut("project/import/{projectid}/{filename}")]
        public async Task<ActionResult<Fileresponse>> ProcessImportFileAsync(
            [FromRoute] int projectid,
            string filename
        )
        {
            return await _service.ImportFileAsync(projectid, filename);
        }

        [HttpPut("sync/{filename}")]
        public async Task<ActionResult<Fileresponse>> ProcessSyncFileAsync(
            [FromRoute] string filename
        )
        {
            return await _service.ImportFileAsync(filename);
        }
    }
}
