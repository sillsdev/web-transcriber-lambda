using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Auth.Models;
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
        }
        private User CurrentUser() { return CurrentUserRepository.GetCurrentUser().Result; }

        public override async Task<IEnumerable<Organization>> GetAsync()
        {
            //scope to user, unless user is super admin
            var isScopeToUser = !CurrentUser().HasRole(RoleName.SuperAdmin);
            IEnumerable<Organization> entities = await base.GetAsync();
            if (isScopeToUser)
            {
                var orgIds = CurrentUser().OrganizationIds.OrEmpty();

                var scopedToUser = entities.Where(organization => orgIds.Contains(organization.Id));

                // return base.Filter(scopedToUser, filterQuery);
                return scopedToUser;
            }

            return entities;
        }
        private string OrgAllGroup(Organization entity)
        {
            return entity.Name + " All";
        }
        public override async Task<Organization> CreateAsync(Organization entity)
        {
            var newEntity = await base.CreateAsync(entity);

            //create an "all org group" and add the current user
            var group = new Group
            {
                Name = OrgAllGroup(newEntity),
                Abbreviation = newEntity.Name.Substring(0, 3) + "All",
                Owner = newEntity,
            };
            var newGroup = await GroupRepository.CreateAsync(group);

            JoinOrg(newEntity, newEntity.Owner, newGroup, RoleName.OrganizationAdmin);
            return newEntity;
        }
        private void JoinOrg(Organization entity, User user, Group allGroup, RoleName role)
        {
            // an org can only be created by the owner of the org. (for now anyway)
            var membership = new OrganizationMembership
            {
                User = user,
                Organization = entity
            };
            var om = OrganizationMembershipRepository.CreateAsync(membership).Result;
            if (allGroup != null)
            {
                var groupmembership = new GroupMembership
                {
                    Group = allGroup,
                    User = user,
                    RoleId = (int)role,
                };
                var gm = GroupMembershipRepository.CreateAsync(groupmembership).Result;
            }
        }
        public bool VerifyOrg(ICurrentUserContext currentUserContext, Organization newOrg)
        {
            /* ask the sil auth if this user has any orgs */
            List<SILAuth_Organization> orgs = currentUserContext.SILOrganizations;
            return orgs.Find(o => o.name == newOrg.Name && o.id == newOrg.SilId) != null;
        }
        private Organization FromSILAuth(SILAuth_Organization entity)
        {
            Organization newEntity = new Organization();
            newEntity.SilId = entity.id;
            newEntity.LogoUrl = entity.logo;
            newEntity.Name = entity.name;
            newEntity.LogoUrl = entity.logo;
            return newEntity;
        }

        public bool JoinOrgs(List<SILAuth_Organization> orgs, User user, RoleName role)
        {
            orgs.ForEach(o => JoinOrg(o, user, role));
            return true;
        }

        private Organization JoinOrg(SILAuth_Organization entity, User user, RoleName role)
        {
            try
            {
                //see if this org exists
                Organization org = FindByNameOrDefault(entity.name);
                if (org == null)
                {
                    //will be added as owner
                    org = CreateAsync(entity, user).Result;
                }
                else if (org.OrganizationMemberships.Where(om => om.UserId == user.Id).ToList().Count == 0)
                {
                    var group = GroupRepository.Get().Where(g => g.Name == OrgAllGroup(org)).FirstOrDefault();

                    JoinOrg(org, user, group, role);
                }
                return org;
            }
            catch (Exception ex)
            {
                throw;
            }
        }
        public async Task<Organization> CreateAsync(SILAuth_Organization entity, User owner)
        {
            Organization newEntity = FromSILAuth(entity);
            newEntity.Owner = owner;
            return await CreateAsync(newEntity);
        }

        public Organization FindByNameOrDefault(string name)
        {
            return MyRepository.Get().Include(o => o.OrganizationMemberships).Where(e => e.Name == name).SingleOrDefault();
        }
        public async Task<Organization> FindByIdOrDefaultAsync(int id)
        {
            return await MyRepository.Get()
                                        .Where(e => e.Id == id)
                                        .FirstOrDefaultAsync();
        }
    }
}