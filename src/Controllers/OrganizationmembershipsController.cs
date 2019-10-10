using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    public class OrganizationmembershipsController : BaseController<OrganizationMembership>
    {
         public OrganizationmembershipsController(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
                IResourceService<OrganizationMembership> resourceService,
            ICurrentUserContext currentUserContext,
            OrganizationService organizationService,
            UserService userService)
          : base(loggerFactory, jsonApiContext, resourceService, currentUserContext, organizationService, userService)
        { }
    }
}