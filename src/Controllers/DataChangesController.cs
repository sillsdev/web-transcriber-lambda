using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DatachangesController : JsonApiController<DataChanges, int>
    {
        DataChangeService service;
        public DatachangesController(
            IJsonApiContext jsonApiContext,
            IResourceService<DataChanges> resourceService)
         : base(jsonApiContext, resourceService)
        {
            service = (DataChangeService)resourceService;
        }
        [HttpGet("since/{since}")]
        public ActionResult GetDataChanges([FromRoute] string since, string origin)
        {
            DateTime dtSince;
            if (!DateTime.TryParse(since, out dtSince))
                return new UnprocessableEntityResult();
            dtSince = dtSince.ToUniversalTime();
            return Ok(service.GetChanges(origin, dtSince));
        }
    }
}