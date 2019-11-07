using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class GroupMembershipRepository : BaseRepository<GroupMembership>
    {
        public GroupMembershipRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          //EntityHooksService<Project> statusUpdateService,
          IDbContextResolver contextResolver
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
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
