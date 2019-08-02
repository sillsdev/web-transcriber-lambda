using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class OrganizationService : BaseArchiveService<Organization>
    {
        public IEntityRepository<OrganizationMembership> OrganizationMembershipRepository { get; }
        public IEntityRepository<UserRole> UserRolesRepository { get; }
        public CurrentUserRepository CurrentUserRepository { get; }
        public IEntityRepository<Group> GroupRepository { get; }
        public IEntityRepository<GroupMembership> GroupMembershipRepository { get; }
        public User CurrentUser { get; }

        public OrganizationService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Organization> organizationRepository,
            IEntityRepository<OrganizationMembership> organizationMembershipRepository,
            IEntityRepository<Group> groupRepository,
            IEntityRepository<GroupMembership> groupMembershipRepository,
            CurrentUserRepository currentUserRepository,
            IEntityRepository<UserRole> userRolesRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, organizationRepository, loggerFactory)
        {
            OrganizationMembershipRepository = organizationMembershipRepository;
            UserRolesRepository = userRolesRepository;
            CurrentUserRepository = currentUserRepository;
            GroupMembershipRepository = groupMembershipRepository;
            GroupRepository = groupRepository;
            CurrentUser = currentUserRepository.GetCurrentUser().Result;
        }

        public override async Task<IEnumerable<Organization>> GetAsync()
        {
            //scope to user, unless user is super admin
            var isScopeToUser = !CurrentUser.HasRole(RoleName.SuperAdmin);
            IEnumerable<Organization> entities = await base.GetAsync();
            if (isScopeToUser)
            {
                var orgIds = CurrentUser.OrganizationIds.OrEmpty();

                var scopedToUser = entities.Where(organization => orgIds.Contains(organization.Id));

                // return base.Filter(scopedToUser, filterQuery);
                return scopedToUser;
            }

            return entities;
        }

        public override async Task<Organization> CreateAsync(Organization entity)
        {
            var newEntity = await base.CreateAsync(entity);

            // an org can only be created by the owner of the org. (for now anyway)
            var membership = new OrganizationMembership
            {
                User = newEntity.Owner,
                Organization = newEntity
            };
            
            await OrganizationMembershipRepository.CreateAsync(membership);

            //create an "all org group" and add the current user
            var group = new Group
            {
                Name = newEntity.Name + " All",
                Abbreviation = newEntity.Name.Substring(0, 3) + "All",
                Owner = newEntity,
            };
            var newGroup = await GroupRepository.CreateAsync(group);
            var groupmembership = new GroupMembership
            {
                Group = newGroup,
                User = newEntity.Owner,
                RoleId = (int) RoleName.OrganizationAdmin,
            };
            await GroupMembershipRepository.CreateAsync(groupmembership);
            return newEntity;
        }

        public async Task<Organization> FindByIdOrDefaultAsync(int id)
        {
            return await MyRepository.Get()
                                        .Where(e => e.Id == id)
                                        .FirstOrDefaultAsync();
        }
    }
}
