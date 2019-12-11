using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class GroupMembershipRepository : BaseRepository<GroupMembership>
    {
        GroupRepository GroupRepository;
        public GroupMembershipRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          IDbContextResolver contextResolver,
          GroupRepository groupRepository
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            GroupRepository = groupRepository;
        }
        private IQueryable<GroupMembership> UsersGroupMemberships(IQueryable<GroupMembership> entities, IQueryable<Group> groups = null)
        {
            if (groups == null)
            {
                groups = GroupRepository.UsersGroups(dbContext.Groups);
            }
            return entities.Join(groups, gm => gm.GroupId, g => g.Id, (gm, g) => gm);
        }
        #region Overrides
        public override IQueryable<GroupMembership> Filter(IQueryable<GroupMembership> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var groups =dbContext.Groups.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersGroupMemberships(entities, groups);
                }
                return UsersGroupMemberships(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersGroupMemberships(entities);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion

        public GroupMembership JoinGroup(int UserId, int groupId, RoleName groupRole)
        {
            GroupMembership groupmembership = Get().Where(gm => gm.GroupId == groupId && gm.UserId == UserId).FirstOrDefault();
            if (groupmembership == null)
            {
                groupmembership = new GroupMembership
                {
                    GroupId = groupId,
                    UserId = UserId,
                    RoleId = (int)groupRole,
                };
                groupmembership = CreateAsync(groupmembership).Result;
            }
            return groupmembership;
        }
    }
}
