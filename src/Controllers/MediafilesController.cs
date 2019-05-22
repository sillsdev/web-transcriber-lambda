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

        [HttpGet("{id}/file")]
        public async Task<IActionResult> GetFile([FromRoute] int id)
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
            entity = await _service.CreateAsync(entity, file);
            return Created("/api/mediafiles/" + entity.Id.ToString(), entity);
        }
        
        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] Mediafile entity)
        {
            throw new JsonApiException((int)HttpStatusCode.NotImplemented, $"Use route /mediafiles/file to Post.  Post must include Form Data: jsonString with entity information, and file with a media file.");
        }
    }
}