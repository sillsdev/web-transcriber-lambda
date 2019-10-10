using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using Microsoft.Extensions.Logging;

namespace SIL.Transcriber.Controllers
{
    public class GroupsController : BaseController<Group>
    {
        public IOrganizationContext OrganizationContext { get; set; }
        public GroupsController(
             ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IResourceService<Group> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }       
    }
}