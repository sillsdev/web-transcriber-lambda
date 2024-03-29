﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Logging.Models;
using SIL.Transcriber.Data;

namespace SIL.Logging.Repositories
{
    public class ParatextSyncRepository : LoggingDbContextRepository<Paratextsync>
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