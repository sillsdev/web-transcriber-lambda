using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class GroupMembershipRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        GroupRepository groupRepository
        ) : BaseRepository<Groupmembership>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        private readonly GroupRepository GroupRepository = groupRepository;

        public IQueryable<Groupmembership> GroupsGroupMemberships(
            IQueryable<Groupmembership> entities,
            IQueryable<Group> groups
        )
        {
            return entities.Join(groups, gm => gm.GroupId, g => g.Id, (gm, g) => gm);
        }

        public IQueryable<Groupmembership> UsersGroupMemberships(
            IQueryable<Groupmembership> entities,
            IQueryable<Group>? groups = null
        )
        {
            groups ??= GroupRepository.UsersGroups(dbContext.Groups);
            return GroupsGroupMemberships(entities, groups);
        }

        public IQueryable<Groupmembership> ProjectGroupMemberships(
            IQueryable<Groupmembership> entities,
            string project
        )
        {
            IQueryable<Group> groups = GroupRepository.ProjectGroups(dbContext.Groups, project);
            return GroupsGroupMemberships(entities, groups);
        }

        public IQueryable<Groupmembership> GetMine(IQueryable<Group> groups)
        {
            return GroupsGroupMemberships(GetAll(), groups)
                .Include(gm => gm.Group)
                .Include(gm => gm.User)
                .Include(gm => gm.Role);
        }

        #region Overrides
        public override IQueryable<Groupmembership> FromCurrentUser(
            IQueryable<Groupmembership>? entities = null
        )
        {
            return UsersGroupMemberships(entities ?? GetAll());
        }

        public override IQueryable<Groupmembership> FromProjectList(
            IQueryable<Groupmembership>? entities,
            string idList
        )
        {
            return ProjectGroupMemberships(entities ?? GetAll(), idList);
        }
        #endregion
    }
}
