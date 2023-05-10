using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

public class OrgKeytermreferencesController : BaseController<Orgkeytermreference>
{
    public OrgKeytermreferencesController(
    ILoggerFactory loggerFactory,
    IJsonApiOptions options,
    IResourceGraph resourceGraph,
    IResourceService<Orgkeytermreference, int> resourceService,
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
