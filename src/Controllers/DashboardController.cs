using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Controllers
{
    //[HttpReadOnly]
    [Route("api/[controller]")]

    public class DashboardsController : JsonApiController<Dashboard, int>
    {
        private readonly DashboardRepository repo;

        public DashboardsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<Dashboard, int> resourceService,
            DashboardRepository repository
        ) : base(
                options,
                resourceGraph,
                loggerFactory,
                resourceService
            )
        {
            repo = repository;
        }

        [AllowAnonymous]
        [HttpGet()]
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<IActionResult> GetAsync(CancellationToken cancelled)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return Ok(repo.GetAll().FirstOrDefault());
        }
    }
}
