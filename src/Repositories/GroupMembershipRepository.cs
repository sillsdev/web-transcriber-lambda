using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class GroupMembershipRepository : BaseRepository<GroupMembership>
    {
        readonly GroupRepository GroupRepository;
        readonly private HttpContext? HttpContext;
        IResourceGraph ResourceGraph;
        public GroupMembershipRepository(
            IHttpContextAccessor  httpContextAccessor,
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            GroupRepository  groupRepository
      ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
          loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            HttpContext = httpContextAccessor.HttpContext;
            GroupRepository = groupRepository;
            ResourceGraph = resourceGraph;
        }
        public IQueryable<GroupMembership> GroupsGroupMemberships(IQueryable<GroupMembership> entities, IQueryable<Group> groups)
        {
            return entities.Join(groups, gm => gm.GroupId, g => g.Id, (gm, g) => gm);
        }
        public IQueryable<GroupMembership> UsersGroupMemberships(IQueryable<GroupMembership> entities, IQueryable<Group>? groups = null)
        {
            if (groups == null)
            {
                groups = GroupRepository.UsersGroups(dbContext.Groups);
            }
            return GroupsGroupMemberships(entities, groups);
        }
        public IQueryable<GroupMembership> ProjectGroupMemberships(IQueryable<GroupMembership> entities, string project)
        {
            IQueryable<Group> groups = GroupRepository.ProjectGroups(dbContext.Groups, project);
            return GroupsGroupMemberships(entities, groups);
        }
        public IQueryable<GroupMembership> GetMine()
        {
            return FromCurrentUser();
        }
        #region Overrides
        public override IQueryable<GroupMembership> FromCurrentUser(IQueryable<GroupMembership>? entities = null)
        {
            return UsersGroupMemberships(entities ?? GetAll());
        }
        protected override IQueryable<GroupMembership> FromProjectList(IQueryable<GroupMembership>? entities, string idList)
        {
            return ProjectGroupMemberships(entities ?? GetAll(), idList);
        }
        #endregion

    }
}
