using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services.Contracts;

namespace SIL.Transcriber.Controllers
{
    /* This has to be a BaseController of some sort to force JsonApiDotNetCore to serialize our objects so they are in right format in the zip file */

    [Route("api/offlineData")]
    public class OfflinedataController(
        ILoggerFactory loggerFactory,
        IOfflineDataService service
        ) : ControllerBase()
    {
        private readonly IOfflineDataService _service = service;
        protected ILogger<OfflinedataController> Logger { get; set; } = loggerFactory.CreateLogger<OfflinedataController>();

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
        //get a signed PUT url to upload the file to the imports folder on s3
        [HttpGet("project/import/{filename}")]
        public ActionResult<Fileresponse> ImportFileUpload([FromRoute] string filename)
        {
            /* get a signed PUT url */
            Fileresponse response = _service.ImportFileURL(filename);
            return response;
        }

        [HttpPut("project/import/{projectid}/{filename}")]
        public async Task<ActionResult<Fileresponse>> ProcessProjectSyncFileAsync(
            [FromRoute] int projectid,
            string filename
        )
        {
            return await _service.ImportSyncFileAsync(projectid, filename, 0);
        }
        [HttpPut("project/import/{projectid}/{filename}/{start}")]
        public async Task<ActionResult<Fileresponse>> ProcessProjectSyncFileAsyncV2(
                                                    [FromRoute] int projectid,
                                                    string filename,
                                                    int start
)
        {
            return await _service.ImportSyncFileAsync(projectid, filename, start);
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

        [HttpPut("project/copyfromfile/{org}/{filename}")]
        [HttpPut("project/copyfromfile/{org}/{filename}/{mapKey}/{start}")]
        public async Task<ActionResult<Fileresponse>> ProcessCopyImportwithOrgFileAsync(
            [FromRoute] int org, string filename, string? mapKey, int? start)
        {
            return await _service.ImportCopyFileIntoOrgAsync(org, filename, start ?? 0, mapKey);
        }
        //DEPRECATED!!
        [HttpPut("project/copy/{neworg}/{filename}")]
        public async Task<ActionResult<Fileresponse>> ProcessCopyImportFileAsync(
            [FromRoute] bool neworg, string filename)
        {
            return await _service.ImportCopyFileAsyncDeprecated(neworg, filename);
        }
        //DEPRECATED!!
        [HttpPut("project/copyp/{neworg}/{projectid}/{mapKey}/{start}")]
        public async Task<ActionResult<Fileresponse>> ProcessCopyImportProjectDAsync(
            [FromRoute] bool neworg, int projectid, string mapKey, int start)
        {
            return await _service.ImportCopyProjectAsyncDeprecated(neworg, projectid, start, mapKey);
        }

        [HttpPut("project/copydata/{org}/{projectid}")]
        [HttpPut("project/copydata/{org}/{projectid}/{mapKey}/{start}")]
        public async Task<ActionResult<Fileresponse>> ProcessCopyImportProjectAsync(
    [FromRoute] int org, int projectid, string? mapKey, int? start)
        {
            return await _service.ImportCopyProjectAsync(org, projectid, start ?? 0, mapKey ?? "");
        }
        //remove the copy project temp data
        [HttpPut("project/copyp/{mapkey}")]
        public Task CopyProjectComplete([FromRoute] string mapkey)
        {
            _service.RemoveCopyProject(mapkey);
            return Task.CompletedTask;
        }
    }
}
