using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.Authorization;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Serialization.Objects;

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
    [Route("api/statehistory")]
    [ApiController]
    public class StatehistoryController : BaseController<Statehistory>
    {
        readonly private StatehistoryService myService;

        public StatehistoryController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            StatehistoryService resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService
        )
            : base(
                loggerFactory,
                options,
                resourceGraph,
                resourceService,
                currentUserContext,
                userService
            )
        {
            myService = (StatehistoryService)resourceService;
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
                throw new JsonApiException(
                    new ErrorObject(System.Net.HttpStatusCode.BadRequest),
                    new Exception($"Invalid Date")
                );
        }
    }
}
