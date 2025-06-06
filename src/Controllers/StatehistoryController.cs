﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //HttpReadOnly]
    [Route("api/statehistory")] //ignored...it's statehistories now

    public class StatehistoryController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        StatehistoryService resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Statehistory>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {
        readonly private StatehistoryService myService = resourceService;

        [AllowAnonymous]
        [HttpGet("since/{since}")]
        public ActionResult<List<Statehistory>> GetSince([FromRoute] string since)
        {
            return DateTime.TryParse(since, out DateTime dateValue)
                ? (ActionResult<List<Statehistory>>)Ok(myService.GetHistorySince(dateValue))
                : throw new JsonApiException(
                    new ErrorObject(System.Net.HttpStatusCode.BadRequest),
                    new Exception($"Invalid Date")
                );
        }
    }
}
