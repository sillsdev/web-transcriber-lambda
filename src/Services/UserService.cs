﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class UserService : BaseArchiveService<User>
    {
        private CurrentUserRepository CurrentUserRepository { get; }
        private UserRepository UserRepository { get; }
        protected readonly AppDbContext dbContext;

        public UserService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<User> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            UserRepository repository,
            AppDbContextResolver contextResolver
        )
            : base(
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
            CurrentUserRepository = currentUserRepository;
            UserRepository = repository;
            dbContext = (AppDbContext)contextResolver.GetContext();
        }

        public User CreateUser(User newUser)
        {
            _ =
                dbContext.Users.Add(newUser);
            _ = dbContext.SaveChanges();
            return newUser;
        }

        public override async Task<User?> UpdateAsync(
            int id,
            User resource,
            CancellationToken cancellationToken
        )
        {
            User? user = await GetAsync(id, cancellationToken);
            if (user == null)
            {
                throw new JsonApiException(
                    new ErrorObject(System.Net.HttpStatusCode.NotFound),
                    new Exception($"User Id '{id}' not found.")
                );
                ;
            }
            return await base.UpdateAsync(id, resource, cancellationToken);
        }

        public User? GetCurrentUser()
        {
            return CurrentUserRepository.GetCurrentUser();
        }

        public async Task<User?> UpdateSharedCreator(string email, bool allowed)
        {
            User? cu = CurrentUserRepository.GetCurrentUser();
            if (cu == null || !(cu.SharedContentAdmin??false))
                throw new Exception("No Update Permission");
            User? user = UserRepository.Get().Where(u => u.Email == email && !u.Archived).FirstOrDefault();
            if (user == null)
                throw new Exception("User Does Not Exist");
            user.SharedContentCreator = allowed;
            return await NoCheckUpdateAsync(user);
        }
    }
}
