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
    public class OrganizationService : EntityResourceService<Organization>
    {
        public IEntityRepository<Organization> OrganizationRepository { get; }
        public IEntityRepository<OrganizationMembership> OrganizationMembershipRepository { get; }
        public CurrentUserRepository currentUserRepository { get; }
        public IEntityRepository<UserRole> UserRolesRepository { get; }
        public User CurrentUser { get; }

        public OrganizationService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Organization> organizationRepository,
            IEntityRepository<OrganizationMembership> organizationMembershipRepository,
            CurrentUserRepository currentUserRepository,
            IEntityRepository<UserRole> userRolesRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, organizationRepository, loggerFactory)
        {
            this.OrganizationRepository = organizationRepository;
            this.OrganizationMembershipRepository = organizationMembershipRepository;
            this.currentUserRepository = currentUserRepository;
            CurrentUser = currentUserRepository.GetCurrentUser().Result;
            UserRolesRepository = userRolesRepository;
        }
        protected bool IsCurrentUserSuperAdmin()
        {
            var userRole = UserRolesRepository.Get()
                .Include(ur => ur.User)
                .Include(ur => ur.Role)
                .Where(ur => ur.UserId == CurrentUser.Id && ur.Role.RoleName == RoleName.SuperAdmin)
                .FirstOrDefault();
            return userRole != null;
        }
        public override async Task<IEnumerable<Organization>> GetAsync()
        {
            //scope to user, unless user is super admin
            var isScopeToUser = !IsCurrentUserSuperAdmin();
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

            return newEntity;
        }

        public async Task<Organization> FindByIdOrDefaultAsync(int id)
        {
            return await OrganizationRepository.Get()
                                               .Where(e => e.Id == id)
                                               .FirstOrDefaultAsync();
        }
    }
}
