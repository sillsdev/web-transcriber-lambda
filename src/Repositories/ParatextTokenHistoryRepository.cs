using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;


namespace SIL.Logging.Repositories
{
    public class ParatextTokenHistoryRepository : LoggingDbContextRepository<ParatextTokenHistory>
    {
        public ParatextTokenHistoryRepository(
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