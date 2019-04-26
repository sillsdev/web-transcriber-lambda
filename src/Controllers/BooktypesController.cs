using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.
    JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class BooktypesController : BaseController<BookType>
    {
         public BooktypesController(
            IJsonApiContext jsonApiContext,
                IResourceService<BookType> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}