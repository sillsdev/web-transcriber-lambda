﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class SectionResourceUserService : BaseArchiveService<SectionResourceUser>
    {
        public SectionResourceUserService(IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<SectionResourceUser> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor)
        {
        }

    }
}