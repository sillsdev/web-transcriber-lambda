using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    //[HttpReadOnly]
    [Route("api/[controller]")]

    public class OrgdatasController(
        ILoggerFactory loggerFactory,
        IJsonApiOptions options,
        IResourceGraph resourceGraph,
        IResourceService<Orgdata, int> resourceService,
        ICurrentUserContext currentUserContext,
        UserService userService
        ) : BaseController<Orgdata>(
            loggerFactory,
            options,
            resourceGraph,
            resourceService,
            currentUserContext,
            userService
            )
    {
    }
}
