﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //[HttpReadOnly]
    [Route("api/[controller]")]
    [ApiController]
    public class ProjdatasController : BaseController<ProjData>
    {
        public ProjdatasController(
             ILoggerFactory loggerFactory,
             IJsonApiOptions options,
             IResourceGraph resourceGraph,
             IResourceService<ProjData,int> resourceService,
             ICurrentUserContext currentUserContext,
   
             UserService userService)
            : base(loggerFactory, options, resourceGraph, resourceService, currentUserContext,  userService)
        {
        }
    }
}