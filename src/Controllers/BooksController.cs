using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class BooksController : BaseController<Book>
    {
        public BooksController(
           IJsonApiContext jsonApiContext,
               IResourceService<Book> resourceService)
         : base(jsonApiContext, resourceService)
        { }
    }
}