﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class VWChecksumService : JsonApiResourceService<VWChecksum, int>
    {
        //private readonly VWChecksumRepository MyRepository;

        public VWChecksumService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<VWChecksum> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor //,
            //VWChecksumRepository repository
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
           // MyRepository = repository;
        }
    }
}
