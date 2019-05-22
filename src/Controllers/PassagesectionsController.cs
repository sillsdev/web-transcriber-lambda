using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class PassagesectionsController : BaseController<Passagesection>
    {
         public PassagesectionsController(
            IJsonApiContext jsonApiContext,
                IResourceService<Passagesection> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }

    }
}