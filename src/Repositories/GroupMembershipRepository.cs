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
using Microsoft.AspNetCore.Http;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class GroupMembershipRepository : BaseRepository<GroupMembership>
    {
        GroupRepository GroupRepository;
        private HttpContext HttpContext;
        public GroupMembershipRepository(
            IHttpContextAccessor httpContextAccessor,
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          AppDbContextResolver contextResolver,
          GroupRepository groupRepository
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            HttpContext = httpContextAccessor.HttpContext;
            GroupRepository = groupRepository;
        }
        public IQueryable<GroupMembership> UsersGroupMemberships(IQueryable<GroupMembership> entities, IQueryable<Group> groups = null)
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
                    IQueryable<Group> groups =dbContext.Groups.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersGroupMemberships(entities, groups);
                }
                return UsersGroupMemberships(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersGroupMemberships(entities);
            }
            if (filterQuery.Has(DATA_START_INDEX)) //ignore
            {
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion

        public GroupMembership JoinGroup(int UserId, int groupId, RoleName groupRole)
        {
            Group group = dbContext.Groups.Find(groupId);
            if (group.Archived) return null;
            GroupMembership groupmembership = Get().Where(gm => gm.GroupId == groupId && gm.UserId == UserId).FirstOrDefault();
            if (groupmembership == null)
            {
                HttpContext.SetFP("api");
                groupmembership = new GroupMembership
                {
                    GroupId = groupId,
                    UserId = UserId,
                    RoleId = (int)groupRole,
                };
                groupmembership = CreateAsync(groupmembership).Result;
            } else if (groupmembership.Archived)
            {
                groupmembership.Archived = false;
                groupmembership = UpdateAsync(groupmembership.Id, groupmembership).Result;
            }
            return groupmembership;
        }
    }
}
