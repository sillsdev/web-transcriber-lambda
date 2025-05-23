﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers;
public class BiblesController(
    ILoggerFactory loggerFactory,
    IJsonApiOptions options,
    IResourceGraph resourceGraph,
    IResourceService<Bible, int> resourceService,
    ICurrentUserContext currentUserContext,
    UserService userService
        ) : BaseController<Bible>(
        loggerFactory,
        options,
        resourceGraph,
        resourceService,
        currentUserContext,
        userService
            )
{
}
