using System;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Forms.GroupMemberships;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class OrganizationMembershipService : EntityResourceService<OrganizationMembership>
    {
        public OrganizationMembershipService(
            IJsonApiContext jsonApiContext,
            UserRepository userRepository,
            ICurrentUserContext currentUserContext,
            IEntityRepository<OrganizationMembership> organizationMembershipRepository,
            IEntityRepository<Role> roleRepository,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, organizationMembershipRepository, loggerFactory)
        {
            UserRepository = userRepository;
            OrganizationMembershipRepository = organizationMembershipRepository;
            RoleRepository = roleRepository;

        }
        private UserRepository UserRepository { get; set; }
        private IEntityRepository<OrganizationMembership> OrganizationMembershipRepository { get; set; }
        private IEntityRepository<Role> RoleRepository { get; }

        public async Task<OrganizationMembership> CreateByEmail(OrganizationMembership membership)
        {
            if (membership.Email == null || membership.OrganizationId == 0) return null;

            User userForEmail = UserRepository.Get()
                .Where(p => p.Email == membership.Email)
                .FirstOrDefault();

            if (userForEmail == null) return null;

            membership.UserId = userForEmail.Id;
            membership.RoleId = (int)RoleName.Member;
            var organizationMembership = await this.MaybeCreateMembership(membership);
         
            return organizationMembership;
        }

        private async Task<OrganizationMembership> MaybeCreateMembership(OrganizationMembership membership)
        {
            var existingMembership = await this.OrganizationMembershipRepository
                .Get()
                .Where(om => (
                    om.UserId == membership.UserId 
                    && om.OrganizationId == membership.OrganizationId
                ))
                .FirstOrDefaultAsync();

            if (existingMembership != null) {
                return existingMembership;
            }

            return await this.OrganizationMembershipRepository.CreateAsync(membership);
        }
    }
}
