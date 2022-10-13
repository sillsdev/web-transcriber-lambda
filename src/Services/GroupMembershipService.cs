using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class GroupMembershipService : BaseArchiveService<Groupmembership>
    {
        readonly private HttpContext? HttpContext;
        private readonly AppDbContext dbContext;

        public GroupMembershipService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Groupmembership> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            GroupMembershipRepository repository,
            IHttpContextAccessor httpContextAccessor,
            AppDbContextResolver contextResolver
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                repository
            )
        {
            HttpContext = httpContextAccessor.HttpContext;
            dbContext = (AppDbContext)contextResolver.GetContext();
        }

        public override async Task<Groupmembership?> CreateAsync(
            Groupmembership entity,
            CancellationToken cancellationToken
        )
        {
            Groupmembership? newEntity = Repo.Get()
                .Include(gm => gm.User)
                .Include(gm => gm.Group)
                .Include(gm => gm.Role)
                .Include(gm => gm.LastModifiedByUser)
                .Where(gm => gm.GroupId == entity.Group.Id && gm.UserId == entity.User.Id)
                .FirstOrDefault();
            if (newEntity == null)
                newEntity = await base.CreateAsync(entity, cancellationToken);
            else
            {
                if (newEntity.Archived)
                {
                    newEntity.Archived = false;
                    _ = await base.UpdateArchivedAsync(newEntity.Id, newEntity, cancellationToken);
                }
            }
            return newEntity;
        }

        public Groupmembership? JoinGroup(int UserId, int groupId, RoleName groupRole)
        {
            Group? group = dbContext.Groups.Where(g => g.Id == groupId).FirstOrDefault();
            if (group?.Archived ?? true)
                return null;
            Groupmembership? groupmembership = dbContext.Groupmemberships
                .Where(gm => gm.GroupId == groupId && gm.UserId == UserId)
                .FirstOrDefault();
            if (groupmembership == null)
            {
                HttpContext?.SetFP("api joingroup");
                groupmembership = new Groupmembership
                {
                    GroupId = groupId,
                    UserId = UserId,
                    RoleId = (int)groupRole,
                };
                _ = dbContext.Groupmemberships.Add(groupmembership);
                //dbContext.SaveChanges();
            }
            else if (groupmembership.Archived)
            {
                groupmembership.Archived = false;
                _ = dbContext.Groupmemberships.Update(groupmembership);
                //dbContext.SaveChanges();
            }
            return groupmembership;
        }
    }
}
