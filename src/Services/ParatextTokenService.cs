using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;
using SIL.Paratext.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class ParatextTokenService : JsonApiResourceService<ParatextToken, int>
    {
        public CurrentUserRepository CurrentUserRepository { get; }
        ParatextTokenRepository TokenRepository;

        public ParatextTokenService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<ParatextToken> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            ParatextTokenRepository tokenRepository)
         : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
            CurrentUserRepository = currentUserRepository;
            TokenRepository = tokenRepository;
        }

    }
}
