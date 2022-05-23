using JsonApiDotNetCore.Configuration;
using SIL.Logging.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Logging.Repositories
{
    public class ParatextSyncRepository : LoggingDbContextRepository<ParatextSync>
    {
        public ParatextSyncRepository(
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