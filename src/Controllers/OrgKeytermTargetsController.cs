using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

public class OrgKeytermTargetsController(
ILoggerFactory loggerFactory,
IJsonApiOptions options,
IResourceGraph resourceGraph,
IResourceService<Orgkeytermtarget, int> resourceService,
ICurrentUserContext currentUserContext,
UserService userService
) : BaseController<Orgkeytermtarget>(
    loggerFactory,
    options,
    resourceGraph,
    resourceService,
    currentUserContext,
    userService
    )
{
}