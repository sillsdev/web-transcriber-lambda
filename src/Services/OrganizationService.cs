using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
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
        public OrganizationMembershipRepository OrganizationMembershipRepository { get; }
        public CurrentUserRepository CurrentUserRepository { get; }
        public GroupRepository GroupRepository { get; }
        public GroupMembershipRepository GroupMembershipRepository { get; }
        private HttpContext HttpContext;

        public OrganizationService(
            IHttpContextAccessor httpContextAccessor,
            IJsonApiContext jsonApiContext,
            IEntityRepository<Organization> organizationRepository,
            OrganizationMembershipRepository organizationMembershipRepository,
            GroupRepository groupRepository,
            GroupMembershipRepository groupMembershipRepository,
            CurrentUserRepository currentUserRepository,
           ILoggerFactory loggerFactory) : base(jsonApiContext, organizationRepository, loggerFactory)
        {
            HttpContext = httpContextAccessor.HttpContext;
            OrganizationMembershipRepository = organizationMembershipRepository;
            CurrentUserRepository = currentUserRepository;
            GroupMembershipRepository = groupMembershipRepository;
            GroupRepository = groupRepository;
        }
        private User CurrentUser() { return CurrentUserRepository.GetCurrentUser().Result; }

        public override async Task<IEnumerable<Organization>> GetAsync()
        {
            //scope to user, unless user is super admin
            var isScopeToUser = !CurrentUser().HasOrgRole(RoleName.SuperAdmin, 0);
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

        public override async Task<Organization> CreateAsync(Organization entity)
        {
            var newEntity = await base.CreateAsync(entity);

            HttpContext.SetFP("api");
            //create an "all org group" and add the current user
            var group = new Group
            {
                Name = entity.AllUsersName ?? "All Users",
                Abbreviation = (newEntity.Name.Length > 3 ? newEntity.Name.Substring(0, 3) : newEntity.Name) + "All",
                Owner = newEntity,
                AllUsers = true,
            };
            var newGroup = await GroupRepository.CreateAsync(group);

            JoinOrg(newEntity, newEntity.Owner, RoleName.Admin, RoleName.Admin);
            return newEntity;
        }
        public void JoinOrg(Organization entity, User user, RoleName orgRole, RoleName groupRole)
        {
            Group allGroup = GroupRepository.Get().Where(g => g.AllUsers && g.OrganizationId == entity.Id).FirstOrDefault();

            if (user.OrganizationMemberships == null || user.GroupMemberships == null)
                user = CurrentUserRepository.Get().Include(o => o.OrganizationMemberships).Include(o => o.GroupMemberships).Where(e => e.Id == user.Id).SingleOrDefault();

            HttpContext.SetFP("api");
            OrganizationMembership membership = user.OrganizationMemberships.Where(om => om.OrganizationId == entity.Id).ToList().FirstOrDefault();
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
                OrganizationMembership om = OrganizationMembershipRepository.CreateAsync(membership).Result;
            }
            else
            {
                if (membership.Archived || membership.RoleName != orgRole)
                {
                    membership.RoleId = (int)orgRole;
                    membership.Archived = false;
                    OrganizationMembership om = OrganizationMembershipRepository.UpdateAsync(membership.Id, membership).Result;
                }
            }
            if (allGroup != null)
            {
                GroupMembershipRepository.JoinGroup(user.Id, allGroup.Id, groupRole);
            }
        }
        public bool VerifyOrg(ICurrentUserContext currentUserContext, Organization newOrg)
        {
            return true;

            /* ask the sil auth if this user has any orgs */
            /*
             * List<SILAuth_Organization> orgs = currentUserContext.SILOrganizations;
             * var silOrg = orgs.Find(o =>o.id == newOrg.SilId);
             * if (silOrg != null)
             * {
             *     /* merge the info *
             * newOrg = SILIdentity.OrgFromSILAuth(newOrg, silOrg);
             *     return true;
             *  }
             * return false;
            */
        }

        /*

        public bool JoinOrgs(List<SILAuth_Organization> orgs, User user, RoleName role)
        {
            orgs.ForEach(o => JoinOrg(o, user, role));
            return true;
        }

        private Organization JoinOrg(SILAuth_Organization entity, User user, RoleName orgRole)
        {
            try
            {
                //see if this org exists
                Organization org = FindByNameOrDefault(entity.name);
                if (org == null)
                {
                    //will be added as owner
                    HttpContext.SetOrigin("api");
                    org = CreateAsync(entity, user).Result;
                }
                else 
                {
                    JoinOrg(org, user, orgRole, RoleName.Transcriber);
                }
                return org;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async Task<Organization> CreateAsync(SILAuth_Organization entity, User owner)
        {
            Organization newEntity = SILIdentity.OrgFromSILAuth(entity);
            newEntity.Owner = owner;
            return await CreateAsync(newEntity);
        }
        */
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