using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //[HttpReadOnly]
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DatachangesController : JsonApiController<Datachanges, int>
    {
        private readonly DataChangeService service;

        public DatachangesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Datachanges, int> resourceService
        ) : base(
                options,
                resourceGraph,
                loggerFactory,
                resourceService
            )
        {
            service = (DataChangeService)resourceService;
        }

        [HttpGet("since/{since}")]
        public ActionResult GetDatachanges([FromRoute] string since, string origin)
        {
            if (!DateTime.TryParse(since, out DateTime dtSince))
                return new UnprocessableEntityResult();
            dtSince = dtSince.ToUniversalTime();
            return Ok(service.GetUserChanges(origin, dtSince));
        }

        [HttpGet("projects/{origin}")]
        public IActionResult GetProjectDatachanges([FromRoute] string origin, string projList)
        {
            ProjDate[]? x = JsonConvert.DeserializeObject<ProjDate[]>(projList);
            return x != null ? Ok(service.GetProjectChanges(origin, x)) : throw new Exception("Project not given.");
            ;
        }

        [HttpGet("v{version}/{start}/since/{since}")]
        public ActionResult GetDatachangesVersion(
            [FromRoute] string version,
            int start,
            string since,
            string origin
        )
        {
            if (!DateTime.TryParse(since, out DateTime dtSince))
                return new UnprocessableEntityResult();
            dtSince = dtSince.ToUniversalTime();
            return Ok(service.GetUserChanges(origin, dtSince, version, start));
        }

        [HttpGet("v{version}/{start}/project/{origin}")]
        public IActionResult GetProjectDatachangesVersion(
            [FromRoute] string version,
            int start,
            string origin,
            string projList
        )
        {
            ProjDate? x = JsonConvert.DeserializeObject<ProjDate>(projList);
            if (x != null && x.id > 0)
            {
                ProjDate?[] pd = { x };
                return Ok(service.GetProjectChanges(origin, pd, version, start));
            }
            throw new Exception("Project not given.");
        }
    }
}
