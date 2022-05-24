using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class PassageStateChangeService : BaseService<PassageStateChange>
    {
        public PassageStateChangeService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<PassageStateChange> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, PassageStateChangeRepository repository) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, 
                  resourceChangeTracker, resourceDefinitionAccessor, repository)
        {
        }
        public Task<PassageStateChange?> CreateAsync(Passage passage, string state, string comment)
        {
            return base.CreateAsync(new PassageStateChange { 
                PassageId = passage.Id, 
                State = state,
                Comments = comment}, new CancellationToken());
        }
    }
    
}
