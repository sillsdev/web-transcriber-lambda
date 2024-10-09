using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

public class VwbiblebrainbibleController : BaseController<Vwbiblebrainbible>
{
    public VwbiblebrainbibleController(
    ILoggerFactory loggerFactory,
    IJsonApiOptions options,
    IResourceGraph resourceGraph,
    IResourceService<Vwbiblebrainbible, int> resourceService,
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
    { }
}
