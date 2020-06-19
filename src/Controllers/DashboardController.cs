﻿using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Controllers;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;
using System.Linq;
using System.Threading.Tasks;

namespace TranscriberAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DashboardsController :  JsonApiController<Dashboard, int>
    {
        DashboardRepository repo;
        public DashboardsController(
             IJsonApiContext jsonApiContext,
             IResourceService<Dashboard> resourceService,
             DashboardRepository repository)
            : base(jsonApiContext, resourceService)
        {
            repo = repository;
        }
       
        [AllowAnonymous]
        [HttpGet()]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<IActionResult> GetAsync()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
           return Ok(repo.Get().FirstOrDefault());
        } 

    }
}
