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
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class OrganizationMembershipService : BaseArchiveService<OrganizationMembership>
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

        public override async Task<IEnumerable<OrganizationMembership>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }

    }
}
