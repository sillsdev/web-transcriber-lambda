using SIL.Transcriber.Models;
using Microsoft.AspNetCore.Mvc;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Services;
using Microsoft.AspNetCore.Authorization;

namespace SIL.Transcriber.Controllers;
[Route("api/[controller]")]
public class BiblebrainbiblesController : BaseController<Biblebrainbible>
{
    public BiblebrainbiblesController(
    ILoggerFactory loggerFactory,
    IJsonApiOptions options,
    IResourceGraph resourceGraph,
    IResourceService<Biblebrainbible, int> resourceService,
    ICurrentUserContext currentUserContext,
    UserService userService
) : base(
        loggerFactory,
        options,
        resourceGraph,
        resourceService,
        currentUserContext,
        userService
    )
    {
    }
    [AllowAnonymous]
    [HttpPost]
    public override Task<IActionResult> PostAsync([FromBody] Biblebrainbible entity, CancellationToken ct) => base.PostAsync(entity, ct);

}
