using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.IO;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    public class MediafilesController : BaseController<Mediafile>
    {
        MediafileService _service;

        public MediafilesController(
            IJsonApiContext jsonApiContext,
            IResourceService<Mediafile> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
            _service = (MediafileService)resourceService;
        }
        //POST will return your new mediafile with a PUT signed url in Audiourl.
        //POST/file expects mediafile and file to be in MultiPartForm. It will upload the file and return new mediafile with nothing in audiourl.
        //GET/{id} will return mediafile record without refreshing Audiourl.
        //GET/{id}/file will return the file directly
        //GET/{id}/fileurl will return mediafile with new GET signed url in Audiourl.

        [HttpGet("{id}/fileurl")]
        public IActionResult GetFile([FromRoute] int id)
        {
            var response = _service.GetFileSignedUrl(id);
            return Ok(response);
        }
        
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
       
        [HttpPost("file")]
        public async Task<IActionResult> PostAsync([FromForm] string jsonString,
                                                    [FromForm] IFormFile file)
        {
            Mediafile entity = JsonConvert.DeserializeObject<Mediafile>(jsonString);
            entity = await _service.CreateAsyncWithFile(entity, file);
            return Created("/api/mediafiles/" + entity.Id.ToString(), entity);
        }
        
    }
}