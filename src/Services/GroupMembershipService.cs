using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class GroupMembershipService : BaseArchiveService<GroupMembership>
    {
        readonly private HttpContext? HttpContext;
        GroupService GroupService;
        public GroupMembershipService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<GroupMembership> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, GroupMembershipRepository repository,
            IHttpContextAccessor httpContextAccessor, GroupService grpService
        ) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor, repository)
        {
            HttpContext = httpContextAccessor.HttpContext;
            GroupService = grpService;
        }

        public override async Task<GroupMembership?> CreateAsync(GroupMembership entity, CancellationToken cancellationToken)
        {
            GroupMembership? newEntity =((IEnumerable<GroupMembership>)GetAsync(cancellationToken)).Where(gm => gm.GroupId == entity.GroupId && gm.UserId == entity.UserId).FirstOrDefault();
            if (newEntity == null)
               newEntity = await base.CreateAsync(entity, cancellationToken);
            else
            {
                if (newEntity.Archived)
                {
                    newEntity.Archived = false;
                    newEntity = base.UpdateAsync(newEntity.Id, newEntity, cancellationToken).Result;
                }

            }
            return newEntity;
        }
        public async Task<GroupMembership?> JoinGroup(int UserId, int groupId, RoleName groupRole)
        {
            CancellationToken ct = new();
            Group? group = GroupService.GetAsync(groupId, ct).Result;
            if (group?.Archived ?? true) return null;
            GroupMembership? groupmembership = GetAsync(ct).Result.Where(gm => gm.GroupId == groupId && gm.UserId == UserId).FirstOrDefault();
            if (groupmembership == null)
            {
                HttpContext?.SetFP("api");
                groupmembership = new GroupMembership
                {
                    GroupId = groupId,
                    UserId = UserId,
                    RoleId = (int)groupRole,
                };
                await CreateAsync(groupmembership, ct);
            }
            else if (groupmembership.Archived)
            {
                groupmembership.Archived = false;
                groupmembership = UpdateAsync(groupmembership.Id, groupmembership, ct).Result;
            }
            return groupmembership;
        }
    }
}
