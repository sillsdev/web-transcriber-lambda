using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BooksController : BaseController<Book>
    {
        public BooksController(
           IJsonApiContext jsonApiContext,
               IResourceService<Book> resourceService)
         : base(jsonApiContext, resourceService)
        { }
    }
}