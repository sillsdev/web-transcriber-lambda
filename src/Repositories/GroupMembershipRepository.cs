using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;

namespace SIL.Transcriber.Repositories
{
    public class GroupMembershipRepository : BaseRepository<Groupmembership>
    {
        readonly GroupRepository GroupRepository;
        readonly private HttpContext? HttpContext;

        public GroupMembershipRepository(
            IHttpContextAccessor httpContextAccessor,
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            GroupRepository groupRepository
        )
            : base(
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
            HttpContext = httpContextAccessor.HttpContext;
            GroupRepository = groupRepository;
        }

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
            if (groups == null)
            {
                groups = GroupRepository.UsersGroups(dbContext.Groups);
            }
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

        public IQueryable<Groupmembership> GetMine()
        {
            return FromCurrentUser()
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
