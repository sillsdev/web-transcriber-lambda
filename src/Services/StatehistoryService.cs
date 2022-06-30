using SIL.Transcriber.Models;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class StatehistoryService : BaseService<Statehistory>
    {
        public StatehistoryService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Statehistory> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            StatehistoryRepository repository
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
            ) { }

        public IEnumerable<Statehistory> GetHistorySince(DateTime since)
        {
            return GetAsync(new CancellationToken()).Result
                .Where(h => h.StateUpdated > since)
                .OrderBy(o => o.Id); //view has an orderby now
        }
    }
}
