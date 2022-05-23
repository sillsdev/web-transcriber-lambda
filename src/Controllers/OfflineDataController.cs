using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Diagnostics;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
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
           IJsonApiOptions options,
           IResourceGraph resourceGraph,
           JsonApiResourceService<FileResponse, int> frService,
           ICurrentUserContext currentUserContext,
           UserService userService,
           IOfflineDataService service)
        :base(loggerFactory, options, resourceGraph,frService, currentUserContext,  userService)
        {
            _service = service;
        }

        [HttpGet("project/export/{id}/{start}")]
        public ActionResult<FileResponse> Export([FromRoute] int id, int start)
        {
            FileResponse response = _service.ExportProjectPTF(id, start);
            return Ok(response);
        }
        [HttpPost("project/export/{exporttype}/{id}/{start}")]
        public ActionResult<JsonedFileResponse> Export([FromRoute] string exportType, int id, int start, [FromForm] string ids, [FromForm] string artifactType)
        {
            FileResponse response;
            Debug.WriteLine(exportType, artifactType, ids);
            switch (exportType)
            {
                case "ptf":
                    response = _service.ExportProjectPTF(id, start);
                    break;
                case "audio":
                    response = _service.ExportProjectAudio(id, artifactType??"",  ids, start);
                    break;
                case "burrito":
                    response = _service.ExportBurrito(id, ids, start);
                    break;
                default:
                    response = _service.ExportProjectPTF(id, start);
                    break;
            }
            //morph this into what we got from the get

            return Ok(response.Twiddle());
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