using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.IO;
using System.Net;
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
        public async Task<Stream> GetFile([FromRoute] int id)
        {
            return await _service.GetFile(id);
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