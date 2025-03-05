using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class ArtifactCategoryService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Artifactcategory> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        ArtifactCategoryRepository repository
        ) : BaseArchiveService<Artifactcategory>(
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
        public override async Task<Artifactcategory?> CreateAsync(
                                    Artifactcategory entity,
                                    CancellationToken cancellationToken)
        {
            if (entity.Organization != null)
            {
                Artifactcategory? newEntity = Repo.Get()
                .Include(ac => ac.Organization)
                .Where(ac => ac.OrganizationId == entity.Organization.Id && 
                        ac.Categoryname == entity.Categoryname && 
                        ac.Note == entity.Note && 
                        ac.Discussion == entity.Discussion && 
                        ac.Resource == entity.Resource)
                .FirstOrDefault();

                if (newEntity != null && newEntity.Archived)
                {
                    newEntity.Archived = false;
                    _ = await base.UpdateArchivedAsync(newEntity.Id, newEntity, cancellationToken);
                    return newEntity;
                }
            }
            return await base.CreateAsync(entity, cancellationToken);
        }
    }
}
