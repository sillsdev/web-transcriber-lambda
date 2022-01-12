using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class UserService : BaseArchiveService<User>
    {
        private ICurrentUserContext CurrentUserContext { get; }
        private CurrentUserRepository CurrentUserRepository { get; }

        public UserService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            CurrentUserRepository currentUserRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, userRepository, loggerFactory)
        {
            CurrentUserContext = currentUserContext;
            CurrentUserRepository = currentUserRepository;
        }

        public override async Task<IEnumerable<User>> GetAsync()
        {
            return await GetScopedToCurrentUser<User>(base.GetAsync,
                                   JsonApiContext);

        }
        public override async Task<User> GetAsync(int id)
        {
            User CurrentUser = CurrentUserRepository.GetCurrentUser().Result;

            if (id == 0) id = CurrentUser.Id;

            if (CurrentUser.Id == id)
            {
                return await base.GetAsync(id);
            }

            IEnumerable<User> users = await GetAsync();

            return users.SingleOrDefault(u => u.Id == id);
        }

        public override async Task<User> UpdateAsync(int id, User resource)
        {
            User user = await GetAsync(id);
            if (user == null)
            {
                throw new JsonApiException(404, $"User Id '{id}' not found."); ;
            }
            return await base.UpdateAsync(id, resource);
        }

        public User GetCurrentUser() {

            return CurrentUserRepository.GetCurrentUser().Result;
        }
    }
}