using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers;

[Route("api/[controller]")]
[AllowAnonymous]
public class BiblebrainsectionsController(
ILoggerFactory loggerFactory,
IJsonApiOptions options,
IResourceGraph resourceGraph,
IResourceService<Biblebrainsection, int> resourceService,
ICurrentUserContext currentUserContext,
UserService userService
) : BaseController<Biblebrainsection>(
    loggerFactory,
    options,
    resourceGraph,
    resourceService,
    currentUserContext,
    userService
    )
{

}


