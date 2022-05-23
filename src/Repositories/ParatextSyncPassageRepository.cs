using JsonApiDotNetCore.Configuration;
using SIL.Logging.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
namespace SIL.Transcriber.Repositories
{
    public class ParatextSyncPassageRepository : LoggingDbContextRepository<ParatextSyncPassage>
    {
        public ParatextSyncPassageRepository(
            ITargetedFields targetedFields, LoggingDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor)
        {
        }
    }
}