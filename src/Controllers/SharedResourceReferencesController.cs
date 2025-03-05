using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

public class SharedresourceReferencesController(
ILoggerFactory loggerFactory,
IJsonApiOptions options,
IResourceGraph resourceGraph,
IResourceService<Sharedresourcereference, int> resourceService,
ICurrentUserContext currentUserContext,
UserService userService
) : BaseController<Sharedresourcereference>(
    loggerFactory,
    options,
    resourceGraph,
    resourceService,
    currentUserContext,
    userService
    )
{
}
