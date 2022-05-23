using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class GroupMembershipService : BaseArchiveService<GroupMembership>
    {
        public GroupMembershipService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<GroupMembership> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor
        ) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
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
    }
}
