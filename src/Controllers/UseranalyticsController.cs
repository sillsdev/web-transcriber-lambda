using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    public class UseranalyticsController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Useranalytic, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Useranalytic>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {
        private readonly UseranalyticService _useranalyticService = (UseranalyticService)resourceService;

        [HttpPost("track")]
        public async Task<IActionResult> Track(CancellationToken cancellationToken)
        {
            User? currentUser = CurrentUser;
            if (currentUser == null)
            {
                return Unauthorized();
            }

            (Useranalytic useranalytic, Countryanalytic countryanalytic) = await _useranalyticService.TrackAsync(currentUser.Id, cancellationToken);
            useranalytic.Country = countryanalytic.Country;
            return Ok(useranalytic);
        }
    }
}