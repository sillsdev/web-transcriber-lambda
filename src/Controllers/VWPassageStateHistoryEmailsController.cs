using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using System.Threading;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Controllers.Annotations;

namespace SIL.Transcriber.Controllers
{
    public class StateChange
    {
        public string Organization { get; set; } = "";
        public string ProjectName { get; set; } = "";
        public string Planname { get; set; } = "";
        public string Passage { get; set; } = "";
        public string Transcriber { get; set; } = "";
        public string Editor { get; set; } = "";
        public string PassageState { get; set; } = "";
        public string StateModifiedby { get; set; } = "";
        public DateTime StateUpdated { get; set; }
        public string Email { get; set; } = "";
    }
    //HttpReadOnly]
    [Route("api/[controller]")]
    [ApiController]
    public class StatehistoryController :BaseController<Vwpassagestatehistoryemail>
    {
        readonly private VwPassageStateHistoryEmailService myService;
        public StatehistoryController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            VwPassageStateHistoryEmailService resourceService,
            ICurrentUserContext currentUserContext,
  
            UserService userService)
            : base(loggerFactory, options, resourceGraph,resourceService, currentUserContext,  userService)
        {
            myService = (VwPassageStateHistoryEmailService)resourceService;
        }
        [AllowAnonymous]
        [HttpGet("since/{since}")]
        public ActionResult<List<StateChange>> GetSince([FromRoute] string since)
        {
            if (DateTime.TryParse(since, out DateTime dateValue))
            {
                return Ok(myService.GetHistorySince(dateValue));
            }
            else
                throw new JsonApiException(new ErrorObject(System.Net.HttpStatusCode.BadRequest), new Exception($"Invalid Date"));
        }
    
    }

}
