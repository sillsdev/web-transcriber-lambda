﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
namespace SIL.Transcriber.Repositories
{
    public class FileresponseRepository(
        ITargetedFields targetedFields, AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph, IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor
        ) : AppDbContextRepository<Fileresponse>(targetedFields, contextResolver, resourceGraph, resourceFactory,
        constraintProviders, loggerFactory, resourceDefinitionAccessor)
    {
    }
}