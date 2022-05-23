using JsonApiDotNetCore.Services;
using SIL.Logging.Models;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Services
{
    public class ParatextTokenHistoryService : JsonApiResourceService<ParatextTokenHistory, int>
    {
        public ParatextTokenHistoryService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<ParatextTokenHistory> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor)
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
        }
    }
}