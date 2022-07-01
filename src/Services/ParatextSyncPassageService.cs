using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using SIL.Logging.Models;

namespace SIL.Transcriber.Services
{
    public class ParatextSyncPassageService : JsonApiResourceService<Paratextsyncpassage, int>
    {
        public ParatextSyncPassageService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Paratextsyncpassage> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor
            )
        { }
    }
}
