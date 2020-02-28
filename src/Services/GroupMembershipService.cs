using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Forms.GroupMemberships;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class GroupMembershipService : BaseArchiveService<GroupMembership>
    {
        public GroupMembershipService(
            IJsonApiContext jsonApiContext,
            UserRepository userRepository,
            ProjectRepository projectRepository,
            ICurrentUserContext currentUserContext,
            IEntityRepository<GroupMembership> groupMembershipRepository,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, groupMembershipRepository, loggerFactory)
        {
            UserRepository = userRepository;
            ProjectRepository = projectRepository;
            CurrentUserContext = currentUserContext;
            GroupMembershipRepository = groupMembershipRepository;
        }

        public UserRepository UserRepository { get; }
        public ProjectRepository ProjectRepository { get; }
        public ICurrentUserContext CurrentUserContext { get; }
        public IEntityRepository<GroupMembership> GroupMembershipRepository { get; }


        public override async Task<IEnumerable<GroupMembership>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }
        public override async Task<GroupMembership> CreateAsync(GroupMembership entity)
        {
            GroupMembership newEntity = GroupMembershipRepository.Get().Where(gm => gm.GroupId == entity.GroupId && gm.UserId == entity.UserId).FirstOrDefault();
            if (newEntity == null)
               newEntity = await base.CreateAsync(entity);
            else
            {
                if (newEntity.Archived)
                {
                    newEntity.Archived = false;
                    newEntity = base.UpdateAsync(newEntity.Id, newEntity).Result;
                }

            }
            return newEntity;
        }

        public override async Task<bool> DeleteAsync(int id)
        {
            var deleteForm = new DeleteForm(UserRepository,
                                            ProjectRepository,
                                            GroupMembershipRepository,
                                            CurrentUserContext);
            if (!deleteForm.IsValid(id))
            {
                throw new JsonApiException(deleteForm.Errors);
            }

            return await base.DeleteAsync(id);
        }
    }
}
