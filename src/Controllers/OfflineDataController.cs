using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Net.Mime;
using System.Threading.Tasks;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OfflinedataController : BaseController<Organization>
    {
        private readonly IOfflineDataService _service;
        public OfflinedataController(
           ILoggerFactory loggerFactory,
           IJsonApiContext jsonApiContext,
           IResourceService<Organization> orgService,
           ICurrentUserContext currentUserContext,
           OrganizationService organizationService,
           UserService userService,
           IOfflineDataService service)
        : base(loggerFactory, jsonApiContext, orgService, currentUserContext, organizationService, userService)
        {
            _service = service;
        }

        [AllowAnonymous]
        [HttpGet("project/{id}")]
        public IActionResult Export([FromRoute] int id)
        {
            var response = _service.ExportProject(id);
                
            Response.Headers.Add("Content-Disposition", new ContentDisposition
            {
                FileName = response.Message,
                Inline = false // false = prompt the user for downloading; true = browser to try to show the file inline
            }.ToString());
            return File(response.FileStream, response.ContentType);
        }
    }
}