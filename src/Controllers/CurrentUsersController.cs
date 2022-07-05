using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Controllers.Annotations;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Controllers
{
    //[HttpReadOnly]
    public class CurrentusersController : BaseController<CurrentUser>
    {
        public CurrentusersController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph, IResourceService<CurrentUser, int> resourceService,
            ICurrentUserContext currentUserContext,
            UserService userService)
         : base(loggerFactory,options,resourceGraph, resourceService, currentUserContext,  userService)
        {
        }
           
    }
}