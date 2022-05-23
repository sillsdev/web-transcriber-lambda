using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class GroupmembershipsController : BaseController<GroupMembership>
    {
        public GroupmembershipsController(
            ILoggerFactory loggerFactory,
            IJsonApiOptions options,
            IResourceGraph resourceGraph,
            IResourceService<GroupMembership,int> resourceService,
            ICurrentUserContext currentUserContext,
  
            UserService userService)
          : base(loggerFactory, options,resourceGraph, resourceService, currentUserContext,  userService)
        { }
     }

}