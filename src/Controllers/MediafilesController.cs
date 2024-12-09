using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Net;
using System.Net.Mime;

namespace SIL.Transcriber.Controllers
{

    public class MediafilesController : BaseController<Mediafile>
    {
        private readonly MediafileService _service;

        public MediafilesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Mediafile, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        ) : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            )
        {
            _service = (MediafileService)resourceService;
        }

        //POST will return your new mediafile with a PUT signed url in Audiourl.
        //POST/file expects mediafile and file to be in MultiPartForm. It will upload the file and return new mediafile with nothing in audiourl.
        //GET/{id} will return mediafile record without refreshing Audiourl.
        //GET/{id}/file will return the file directly
        //GET/{id}/fileurl will return mediafile with new GET signed url in Audiourl.
        //AllowAnonymous
        //GET/"fromfile/{s3File}" will return mediafile associated with s3 filename
        //PATCH/{id}/fileinfo/{filesize}/{duration}") will update filesize and duration in mediafile
        //called from s3 trigger - no auth
        [AllowAnonymous]
        [HttpGet("fromfile/{plan}/{s3File}")]
        public IActionResult GetFromFile(
            [FromRoute] int plan,
            [FromRoute] string s3File
        )
        {
            Mediafile? response = _service.GetFromFile(plan, s3File);
            return response == null ? NotFound() : Ok(response);
        }
        [AllowAnonymous]
        [HttpGet("fromfile/{plan}/{s3File}/{segments}")]
        public IActionResult GetFromFileSegments(
            [FromRoute] int plan,
            [FromRoute] string s3File,
            [FromRoute] string segments
)
        {
            Mediafile? response = _service.GetFromFile(plan, s3File, segments);
            return response == null ? NotFound() : Ok(response);
        }

        [Authorize]
        [HttpGet("{id}/fileurl")]
        public IActionResult GetFile([FromRoute] int id)
        {

            Mediafile? response = _service.GetFileSignedUrl(id);
            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetFileDirect([FromRoute] int id)
        {
            S3Response response = await _service.GetFile(id);

            if (response.Status == HttpStatusCode.OK && response.FileStream != null)
            {
                Response.Headers.Add(
                    "Content-Disposition",
                    new ContentDisposition
                    {
                        FileName = response.Message,
                        Inline = true // false = prompt the user for downloading; true = browser to try to show the file inline
                    }.ToString()
                );

                return File(response.FileStream, response.ContentType);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("{id}/eaf")]
        public IActionResult GetEaf([FromRoute] int id)
        {
            Mediafile? mf = _service.GetAsync(id, new CancellationToken()).Result;
            string response = mf != null ? _service.EAF(mf) : "";

            return Ok(response);
        }

        [HttpDelete("{id}/file")]
        public async Task<IActionResult> DeleteFile([FromRoute] int id)
        {
            S3Response response = await _service.DeleteFile(id);
            return response.Status is HttpStatusCode.OK or HttpStatusCode.NoContent ? Ok() : NotFound();
        }


        [HttpGet("wbt")]
        public IActionResult WBTUpdateAsync()
        {
            return Ok(_service.WBTUpdate());
        }
        [AllowAnonymous]
        [HttpPatch("{id}/publish")]
        public async Task<IActionResult> PublishMediafileAsync([FromRoute] int id, [FromBody] Mediafile media)
        {
            try
            {
                return Ok(await _service.PublishM(id, media));
            }
            catch
            {
                return NotFound();
            }
        }

        [AllowAnonymous]
        [HttpPatch("{id}/fileinfo/{filesize}/{duration}")]
        public async Task<IActionResult> UpdateFileInformationAsync(
            [FromRoute] int id,
            [FromRoute] long filesize,
            [FromRoute] decimal duration
        )
        {
            Mediafile? mf = await _service.UpdateFileInfoAsync(id, filesize, duration);
            return mf != null ? Ok(mf) : NotFound();
        }

        [HttpGet("{id}/noiseremoval")]
        public async Task<IActionResult> NoiseRemovalAsync([FromRoute] int id)
        {
            Mediafile? mf = await _service.NoiseRemovalAsync(id);
            return mf?.TextQuality != null ? Ok(mf) : NotFound();
        }

        [HttpGet("{id}/noiseremoval/{taskId}")]
        public async Task<IActionResult> NoiseRemovalStatusAsync([FromRoute] int id, [FromRoute] string taskId)
        {
            Mediafile? mf = await _service.NoiseRemovalStatusAsync(id, taskId);
            //probably need to have a status or class returned...
            return Ok(mf);
        }
    }
}
