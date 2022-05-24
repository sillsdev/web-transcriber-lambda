using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;

using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class OrganizationService : BaseArchiveService<Organization>
    {
        public OrganizationMembershipService OrganizationMembershipService { get; }
        public CurrentUserRepository CurrentUserRepository { get; }
        public GroupRepository GroupRepository { get; }
        public GroupMembershipService GroupMembershipService { get; }
        readonly private HttpContext? HttpContext;
        public OrganizationService(IHttpContextAccessor httpContextAccessor,
            IResourceRepositoryAccessor repositoryAccessor, 
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<Organization> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository, 
            GroupRepository groupRepository,
            GroupMembershipService groupMembershipService,
            OrganizationMembershipService organizationMembershipService,
            OrganizationRepository repository) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor,repository)
        {
            HttpContext = httpContextAccessor.HttpContext;
            OrganizationMembershipService = organizationMembershipService;
            CurrentUserRepository = currentUserRepository;
            GroupMembershipService = groupMembershipService;
            GroupRepository = groupRepository;
        }
        private User? CurrentUser() { return CurrentUserRepository.GetCurrentUser(); }

        public void JoinOrg(Organization entity, User user, RoleName orgRole, RoleName groupRole)
        {
            Group? allGroup = GroupRepository.Get().Where(g => g.AllUsers && g.OrganizationId == entity.Id).FirstOrDefault();

            if (user.OrganizationMemberships == null || user.GroupMemberships == null)
            { 
                User? cu = CurrentUser();
                if (cu != null)
                    user = cu;
            }
            if (HttpContext != null) HttpContext.SetFP("api");
            OrganizationMembership? membership = user?.OrganizationMemberships?.Where(om => om.OrganizationId == entity.Id).ToList().FirstOrDefault();
            if (user == null) return;
            if (membership == null)
            {
                membership = new OrganizationMembership
                {
                    User = user,
                    UserId = user.Id,
                    Organization = entity,
                    OrganizationId = entity.Id,
                    RoleId = (int)orgRole
                };
                OrganizationMembership? om = OrganizationMembershipService.CreateAsync(membership, new CancellationToken()).Result;
            }
            else
            {
                if (membership.Archived || membership.RoleName != orgRole)
                {
                    membership.RoleId = (int)orgRole;
                    membership.Archived = false;
                    OrganizationMembership? om = OrganizationMembershipService.UpdateAsync(membership.Id, membership, new CancellationToken()).Result;
                }
            }
            if (allGroup != null)
            {
                _ = GroupMembershipService.JoinGroup(user.Id, allGroup.Id, groupRole).Result;
            }
        }
    }
}