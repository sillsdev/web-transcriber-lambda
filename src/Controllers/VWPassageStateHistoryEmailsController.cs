    using JsonApiDotNetCore.Services;
    using Microsoft.AspNetCore.Mvc;
    using SIL.Transcriber.Models;
    using SIL.Transcriber.Services;
    using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System.Linq;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Controllers
{
    public class StateChange
    {
        public string Organization { get; set; }
        public string ProjectName { get; set; }
        public string Planname { get; set; }
        public string Passage { get; set; }
        public string Transcriber { get; set; }
        public string Editor { get; set; }
        public string PassageState { get; set; }
        public string StateModifiedby { get; set; }
        public DateTime StateUpdated { get; set; }
        public string Email { get; set; }
    }
    [Route("api/[controller]")]
    [ApiController]
    public class StatehistoryController :BaseController<VwPassageStateHistoryEmail>
    {
        private VwPassageStateHistoryEmailService myService;
        public StatehistoryController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            VwPassageStateHistoryEmailService resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
            : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        {
            myService = (VwPassageStateHistoryEmailService)resourceService;
        }
        [AllowAnonymous]
        [HttpGet("since/{since}")]
        public ActionResult<List<StateChange>> GetSince([FromRoute] string since)
        {
            DateTime dateValue;
            if (DateTime.TryParse(since, out dateValue))
            {
                return Ok(myService.GetHistorySince(dateValue));
            }
            else
                throw new JsonApiException(400, $"Invalid Date");
        }
        
#pragma warning disable 1998
        [HttpPost]
        public override async Task<IActionResult> PostAsync([FromBody] VwPassageStateHistoryEmail entity)
        {
            throw new JsonApiException(405, $"Not implemented for State History.");
        }
#pragma warning restore 1998

#pragma warning disable 1998
        [HttpPatch("{id}")]
        public override async Task<IActionResult> PatchAsync(int id, [FromBody] VwPassageStateHistoryEmail entity)
        {
            throw new JsonApiException(405, $"Not implemented for State History.");
        }
#pragma warning restore 1998
#pragma warning disable 1998
        [HttpDelete("{id}")]
        public override async Task<IActionResult> DeleteAsync(int id)
        {
            throw new JsonApiException(405, $"Not implemented for State History.");
        }
#pragma warning restore 1998
    
    }

}
