using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class MediafilesController : BaseController<Mediafile>
    {
        MediafileService _service;

        public MediafilesController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<Mediafile> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
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

        [HttpGet("{id}/fileurl")]
        public IActionResult GetFile([FromRoute] int id)
        {
            var response = _service.GetFileSignedUrl(id);
            return Ok(response);
        }

        [AllowAnonymous]
        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetFileDirect([FromRoute] int id)
        {
            var response = await _service.GetFile(id);

            if (response.Status == HttpStatusCode.OK)
            {
                Response.Headers.Add("Content-Disposition", new ContentDisposition
                {
                    FileName = response.Message,
                    Inline = true // false = prompt the user for downloading; true = browser to try to show the file inline
                }.ToString());

                return File(response.FileStream, response.ContentType);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpDelete("{id}/file")]
        public async Task<IActionResult> DeleteFile([FromRoute] int id)
        {
            S3Response response = await _service.DeleteFile(id);
            if (response.Status == HttpStatusCode.OK || response.Status == HttpStatusCode.NoContent)
                return Ok();
            else
                return NotFound();
        }

        [HttpPost("file")]
        public async Task<IActionResult> PostAsync([FromForm] string jsonString,
                                                    [FromForm] IFormFile file)
        {
            Mediafile entity = JsonConvert.DeserializeObject<Mediafile>(jsonString);
            entity = await _service.CreateAsyncWithFile(entity, file);
            return Created("/api/mediafiles/" + entity.Id.ToString(), entity);
        }

        //called from s3 trigger - no auth
        [AllowAnonymous]
        [HttpGet("fromfile/{s3File}")]
        public async Task<IActionResult> GetFromFile([FromRoute] string s3File)
        {
            var response = await _service.GetFromFile(s3File);
            if (response == null)
                return NotFound();
            return Ok(response);

        }
        [AllowAnonymous]
        [HttpPatch("{id}/fileinfo/{filesize}/{duration}")]
        public async Task<IActionResult> UpdateFileInformationAsync([FromRoute] int id, [FromRoute] long filesize, [FromRoute] decimal duration)
        {
            Mediafile mf = await _service.UpdateFileInfo(id, filesize, duration);
            if (mf == null)
                return NotFound();
            return Ok(mf);
        }

    }
}