using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

public class VwbiblebrainlanguageController : BaseController<Vwbiblebrainlanguage>
{
    public VwbiblebrainlanguageController(
    ILoggerFactory loggerFactory,
    IJsonApiOptions options,
    IResourceGraph resourceGraph,
    IResourceService<Vwbiblebrainlanguage, int> resourceService,
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
