using SIL.Transcriber.Models;
using Microsoft.AspNetCore.Mvc;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Services;
using Microsoft.AspNetCore.Authorization;

namespace SIL.Transcriber.Controllers;
[Route("api/[controller]")]
public class BiblebrainbiblesController(
ILoggerFactory loggerFactory,
IJsonApiOptions options,
IResourceGraph resourceGraph,
IResourceService<Biblebrainbible, int> resourceService,
ICurrentUserContext currentUserContext,
UserService userService
) : BaseController<Biblebrainbible>(
    loggerFactory,
    options,
    resourceGraph,
    resourceService,
    currentUserContext,
    userService
    )
{
    [AllowAnonymous]
    [HttpPost]
    public override Task<IActionResult> PostAsync([FromBody] Biblebrainbible entity, CancellationToken ct) => base.PostAsync(entity, ct);

}
