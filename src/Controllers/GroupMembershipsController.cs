using JsonApiDotNetCore.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
namespace SIL.Transcriber.Controllers
{
    public class GroupMembershipsController : BaseController<GroupMembership>
    {
        public IOrganizationContext OrganizationContext { get; set; }
        public GroupMembershipsController(
            IJsonApiContext jsonApiContext,
            IResourceService<GroupMembership> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
     }

}