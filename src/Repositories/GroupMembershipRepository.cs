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
        readonly GroupMembershipService GMService;
        IResourceGraph ResourceGraph;
        public GroupMembershipRepository(
            IHttpContextAccessor  httpContextAccessor,
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            GroupRepository  groupRepository,
            GroupMembershipService gmService
      ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
          loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            HttpContext = httpContextAccessor.HttpContext;
            GroupRepository = groupRepository;
            GMService = gmService;
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
        protected override IQueryable<GroupMembership> GetAll()
        {
            return FromCurrentUser();
        }
        protected override IQueryable<GroupMembership> FromCurrentUser(QueryLayer? layer = null)
        {
            return UsersGroupMemberships(base.GetAll());
        }
        protected override IQueryable<GroupMembership> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectGroupMemberships(base.GetAll(), idList);
        }
        #endregion

        public async Task<GroupMembership?>  JoinGroup(int UserId, int groupId, RoleName groupRole)
        {
            Group? group = dbContext.Groups.Find(groupId);
            if (group?.Archived??true) return null;
            GroupMembership? groupmembership = GetAll().Where(gm => gm.GroupId == groupId && gm.UserId == UserId).FirstOrDefault();
            CancellationToken ct = new();
            if (groupmembership == null)
            {
                HttpContext?.SetFP("api");
                groupmembership = new GroupMembership
                {
                    GroupId = groupId,
                    UserId = UserId,
                    RoleId = (int)groupRole,
                };
                await GMService.CreateAsync(groupmembership, ct);
            } else if (groupmembership.Archived)
            {
                groupmembership.Archived = false;
                groupmembership = GMService.UpdateAsync(groupmembership.Id, groupmembership, ct).Result;
            }
            return groupmembership;
        }
    }
}
