﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class DiscussionService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Discussion> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        DiscussionRepository repository
        ) : BaseArchiveService<Discussion>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor,
            repository
            )
    {
        private readonly CurrentUserRepository CurrentUserRepository = currentUserRepository;

        public override async Task<Discussion?> CreateAsync(
                                    Discussion resource,
                                    CancellationToken cancellationToken
                                )
        {

            resource.CreatorUserId ??= CurrentUserRepository.GetCurrentUser()?.Id;
            return await base.CreateAsync(resource, cancellationToken);
        }
    }
}
