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
            [FromForm] string? artifactType,
            [FromForm] string? nameTemplate
        )
        {
            Fileresponse response = exportType switch
            {
                "ptf" => _service.ExportProjectPTF(id, start),
                "audio" => _service.ExportProjectAudio(id, artifactType ?? "", ids, start, false, nameTemplate),
                "elan" => _service.ExportProjectAudio(id, artifactType ?? "", ids, start, true, nameTemplate),
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
            return await _service.ImportFileAsync(projectid, filename, 0);
        }
        [HttpPut("project/import/{projectid}/{filename}/{start}")]
        public async Task<ActionResult<Fileresponse>> ProcessImportFileAsyncV2(
                                                    [FromRoute] int projectid,
                                                    string filename,
                                                    int start
)
        {
            return await _service.ImportFileAsync(projectid, filename, start);
        }

        [HttpPut("sync/{filename}")]
        public async Task<ActionResult<Fileresponse>> ProcessSyncFileAsync(
            [FromRoute] string filename
        )
        {
            return await _service.ImportSyncFileAsync(filename, 0, 0);
        }
        [HttpPut("sync/{filename}/{fileIndex}/{start}")]
        public async Task<ActionResult<Fileresponse>> ProcessSyncFileAsyncV2([FromRoute] string filename, 
            int fileIndex, int start)
        {
            return await _service.ImportSyncFileAsync(filename, fileIndex, start);
        }

        [HttpPut("project/copy/{neworg}/{filename}")]
        public async Task<ActionResult<Fileresponse>> ProcessCopyImportFileAsync(
            [FromRoute] bool neworg, string filename)
        {
            return await _service.ImportCopyFileAsync(neworg, filename);
        }
        [HttpPut("project/copyp/{neworg}/{projectid}/{start}")]
        [HttpPut("project/copyp/{neworg}/{projectid}/{start}/{newProjId}")]
        public async Task<ActionResult<Fileresponse>> ProcessCopyImportProjectAsync(
    [FromRoute] bool neworg, int projectid, int start, int? newProjId)
        {
            return await _service.ImportCopyProjectAsync(neworg, projectid, start, newProjId);
        }
        [HttpPut("project/copyp/{newProjId}")]
        public Task CopyProjectComplete([FromRoute] int newProjId)
        {
            _service.RemoveCopyProject(newProjId);
            return Task.CompletedTask;
        }
    }
}
