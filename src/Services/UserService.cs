using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Services
{
    public class UserService : BaseArchiveService<User>
    {
        private CurrentUserRepository CurrentUserRepository { get; }

        public UserService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<User> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository, UserRepository repository
            ) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor, repository)
        {
            CurrentUserRepository = currentUserRepository;
        }

        public override async Task<User?> GetAsync(int id, CancellationToken cancellationToken)
        {
            User? CurrentUser = CurrentUserRepository.GetCurrentUser();
            if (id == 0 && CurrentUser != null) 
                id = CurrentUser.Id;

            if (CurrentUser?.Id == id)
            {
                return CurrentUser;
            }

            return await base.GetAsync(id, cancellationToken);
        }

        public override async Task<User?> UpdateAsync(int id, User resource, CancellationToken cancellationToken)
        {
            User? user = await GetAsync(id, cancellationToken);
            if (user == null)
            {
                throw new JsonApiException(new ErrorObject(System.Net.HttpStatusCode.NotFound), new Exception($"User Id '{id}' not found.")); ;
            }
            return await base.UpdateAsync(id, resource, cancellationToken);
        }

        public User? GetCurrentUser()
        {
            return CurrentUserRepository.GetCurrentUser();
        }
    }
}