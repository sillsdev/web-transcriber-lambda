using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class GroupmembershipsController : BaseController<GroupMembership>
    {
        public IOrganizationContext OrganizationContext { get; set; }
        public GroupmembershipsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<GroupMembership> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
     }

}