using SIL.Transcriber.Models;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class VwPassageStateHistoryEmailService : BaseService<VwPassageStateHistoryEmail>
    {
        public VwPassageStateHistoryEmailService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<VwPassageStateHistoryEmail> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, VwPassageStateHistoryEmailRepository repository) : 
            base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor,repository)
        {
        }
        public IEnumerable<VwPassageStateHistoryEmail> GetHistorySince(DateTime since)
        {
            return GetAsync(new CancellationToken()).Result.Where(h => h.StateUpdated > since).OrderBy(o => o.Id);   //view has an orderby now 
        }
    }
}