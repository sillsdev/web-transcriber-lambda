using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Forms.Groups;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class GroupService : BaseArchiveService<Group>
    {
        public IOrganizationContext OrganizationContext { get; private set; }
        public ICurrentUserContext CurrentUserContext { get; }
        public UserRepository UserRepository { get; }
        public IEntityRepository<UserRole> UserRolesRepository { get; }

        public GroupService(
            IJsonApiContext jsonApiContext,
            IOrganizationContext organizationContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            IEntityRepository<Group> groupRepository,
            IEntityRepository<UserRole> userRolesRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, groupRepository,  loggerFactory)
        {
            OrganizationContext = organizationContext;
            CurrentUserContext = currentUserContext;
            UserRepository = userRepository;
            UserRolesRepository = userRolesRepository;
        }


        public override async Task<IEnumerable<Group>> GetAsync()
        {
            return await GetScopedToOrganization<Group>(base.GetAsync,
                                                        OrganizationContext,
                                                        JsonApiContext);
        }
        public override async Task<Group> GetAsync(int id)
        {
            var groups = await GetAsync();
            return groups.SingleOrDefault(g => g.Id == id);
        }
        public override async Task<Group> UpdateAsync(int id, Group resource)
        {
            var updateForm = new UpdateForm(UserRepository,
                                             (GroupRepository)MyRepository,
                                             OrganizationContext,
                                             UserRolesRepository,
                                             CurrentUserContext);
            if (!updateForm.IsValid(id, resource))
            {
                throw new JsonApiException(updateForm.Errors);
            }
            return await base.UpdateAsync(id, resource);
        }
    }
}