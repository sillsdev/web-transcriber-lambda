using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    public class BooktypesController : BaseController<BookType>
    {
         public BooktypesController(
            IJsonApiContext jsonApiContext,
                IResourceService<BookType> resourceService)
          : base(jsonApiContext, resourceService)
        { }
    }
}