using System;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //[HttpReadOnly]
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DatachangesController : JsonApiController<DataChanges, int>
    {
        readonly DataChangeService service;

        public DatachangesController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<DataChanges,int> resourceService)
         : base(options, resourceGraph, loggerFactory, resourceService)
        {
            service = (DataChangeService)resourceService;
        }
        [HttpGet("since/{since}")]
        public ActionResult GetDataChanges([FromRoute] string since, string origin)
        {
            if (!DateTime.TryParse(since, out DateTime dtSince))
                return new UnprocessableEntityResult();
            dtSince = dtSince.ToUniversalTime();
            return Ok(service.GetUserChanges(origin, dtSince));
        }

        [HttpGet("projects/{origin}")]
        public IActionResult GetProjectDataChanges([FromRoute] string origin, string projList)
        {
            ProjDate[]? x = JsonConvert.DeserializeObject<ProjDate[]>(projList);
            if (x != null)
                return Ok(service.GetProjectChanges(origin, x));
            return BadRequest();
        }
        [HttpGet("v{version}/{start}/since/{since}")]
        public ActionResult GetDataChangesVersion([FromRoute] string version,int start, string since, string origin)
        {
            if (!DateTime.TryParse(since, out DateTime dtSince))
                return new UnprocessableEntityResult();
            dtSince = dtSince.ToUniversalTime();
            return Ok(service.GetUserChanges(origin, dtSince, version, start));
        }

        [HttpGet("v{version}/{start}/project/{origin}")]
        public IActionResult GetProjectDataChangesVersion([FromRoute] string version,int start,string origin, string projList)
        {
            ProjDate? x = JsonConvert.DeserializeObject<ProjDate>(projList);
            ProjDate?[] pd ={ x };
            return Ok(service.GetProjectChanges(origin, pd, version, start));
        }
    }
}