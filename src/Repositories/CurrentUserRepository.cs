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
            IEntityRepository<UserRole> userRolesRepository,
            ICurrentUserContext currentUserContext
       ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
            this.DBContext = (AppDbContext)contextResolver.GetContext();
            this.CurrentUserContext = currentUserContext;
            UserRolesRepository = userRolesRepository;
        }

        private AppDbContext DBContext { get; }
        private ICurrentUserContext CurrentUserContext { get; }
        private IEntityRepository<UserRole> UserRolesRepository { get; }

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
                .Include(user => user.GroupMemberships)
               .Include(user => user.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstOrDefaultAsync();

            return currentUser;
        }
        public bool IsSuperAdmin(User currentuser)
        {
            return currentuser.HasRole(RoleName.SuperAdmin);
        }
        public bool IsOrgAdmin(User currentuser, int orgId)
        {
            return currentuser.HasRole(RoleName.OrganizationAdmin, orgId);
        }
    }
}