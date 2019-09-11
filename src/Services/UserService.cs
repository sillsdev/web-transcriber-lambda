using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
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
        private IOrganizationContext OrganizationContext { get; }
        private ICurrentUserContext CurrentUserContext { get; }
        private CurrentUserRepository CurrentUserRepository { get; }
        private User CurrentUser { get; }

        public UserService(
            IJsonApiContext jsonApiContext,
            IOrganizationContext organizationContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            CurrentUserRepository currentUserRepository,
            IEntityRepository<User> entityRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, entityRepository, loggerFactory)
        {
            OrganizationContext = organizationContext;
            CurrentUserContext = currentUserContext;
            CurrentUserRepository = currentUserRepository;
            CurrentUser= currentUserRepository.GetCurrentUser().Result;
        }

        

        public override async Task<IEnumerable<User>> GetAsync()
        {
            return await GetScopedToOrganization<User>(base.GetAsync,
                                   OrganizationContext,
                                   JsonApiContext);

        }
        public override async Task<User> GetAsync(int id)
        {
            
            if (id == 0) id = CurrentUser.Id;

            if (CurrentUser.Id == id)
            {
                return await base.GetAsync(id);
            }
            
            var users = await GetAsync();

            return users.SingleOrDefault(u => u.Id == id);
        }

        public override async Task<User> UpdateAsync(int id, User resource)
        {
            var user = await GetAsync(id);
            if (user == null)
            {
                throw new JsonApiException(404, $"User Id '{id}' not found."); ;
            }
            return await base.UpdateAsync(id, resource);
        }

        public async Task<User> GetCurrentUser() {
            
            if (null == CurrentUser) return null;

            return await base.GetAsync(CurrentUser.Id);
        }
    }
}