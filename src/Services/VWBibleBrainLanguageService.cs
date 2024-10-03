using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class VWBibleBrainLanguageService : JsonApiResourceService<Vwbiblebrainlanguage, int>
    {
        public VWBibleBrainLanguageService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Vwbiblebrainlanguage> resourceChangeTracker,
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
        {
        }
    }
}