using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class BibleBrainBibleService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Biblebrainbible> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        BibleBrainBibleRepository repository
        ) : BaseService<Biblebrainbible>(
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
        public override async Task<Biblebrainbible?> CreateAsync(
                                        Biblebrainbible resource,
                                        CancellationToken cancellationToken
                                    )
        {
            //if we already have it, just return it.
            Biblebrainbible? x = resource.BibleId == null ? null : Repo.Get().Where(t =>
                        t.BibleId == resource.BibleId
                )
                .FirstOrDefault();
            return x ?? await base.CreateAsync(resource, cancellationToken);

        }
    }
}
