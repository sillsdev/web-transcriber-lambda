using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class CurrentUserRepository : DefaultEntityRepository<User> 
    {
        // NOTE: this repository MUST not rely on any other repositories or services
        public CurrentUserRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            IDbContextResolver contextResolver,
            ICurrentUserContext currentUserContext
        ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
            this.DBContext = (AppDbContext)contextResolver.GetContext();
            this.CurrentUserContext = currentUserContext;
        }

        public AppDbContext DBContext { get; }
        public ICurrentUserContext CurrentUserContext { get; }

        // memoize once per local thread,
        // since the current user can't change in a single request
        // this should be ok.
        public async Task<User> GetCurrentUser()
        {
            var auth0Id = this.CurrentUserContext.Auth0Id; // RUNNING W/O AUTH! ?? "107708962143422938734";

            var userFromResult = this.DBContext
                .Users.Local
                .FirstOrDefault(u => u.ExternalId.Equals(auth0Id));

            if (userFromResult != null) {
                return await System.Threading.Tasks.Task.FromResult(userFromResult);
            }

            var currentUser = await base.Get()
                .Where(user => user.ExternalId.Equals(auth0Id))
                .Include(user => user.OrganizationMemberships)
                .Include(user => user.UserRoles)
                .FirstOrDefaultAsync();

            try {
                //SJH WHAT TO DO HERE????
                //await DWKitRuntime.Security.SignInAsync(currentUser?.Email, remember: false);
            } catch { //(System.Exception e) {
                // do nothing for now, we want normal JWT sign in to work always
                // if there is an exeption, it means the users
                // have not yet been sync'd
            }
            
            return currentUser;
        }
    }
}