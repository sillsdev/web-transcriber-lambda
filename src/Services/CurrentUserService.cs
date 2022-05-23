using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;

using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class CurrentUserService : BaseService<CurrentUser>
    {
        private readonly CurrentUserRepository CurrentUserRepository;

        public CurrentUserService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<CurrentUser> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, CurrentUserRepository currentUserRepository)
        : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
            CurrentUserRepository = currentUserRepository;
        }
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public override async Task<IReadOnlyCollection<CurrentUser>> GetAsync(CancellationToken cancellationToken)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            return new List<CurrentUser> { CurrentUserRepository.Get() };
        }
    }
}