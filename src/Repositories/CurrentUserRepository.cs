using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Repositories
{
    public class CurrentUserRepository : DefaultEntityRepository<User>
    {
        // NOTE: this repository MUST not rely on any other repositories or services
        public CurrentUserRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            AppDbContextResolver contextResolver,
            ICurrentUserContext currentUserContext
       ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
            this.DBContext = (AppDbContext)contextResolver.GetContext();
            this.CurrentUserContext = currentUserContext;
            this.Logger = loggerFactory.CreateLogger<User>();
        }

        private AppDbContext DBContext { get; }
        private ICurrentUserContext CurrentUserContext { get; }
        protected ILogger<User> Logger { get; set; }
        private User curUser;
        // memoize once per local thread,
        // since the current user can't change in a single request
        // this should be ok.
        public User GetCurrentUser(bool checkForUpdate = false)
        {
            if (curUser == null)
            {
                string auth0Id = GetVarOrDefault("SIL_TR_DEBUGUSER", this.CurrentUserContext.Auth0Id);

                User currentUser = Get()
                    .Where(user => !user.Archived && user.ExternalId.Equals(auth0Id))
                    .Include(user => user.OrganizationMemberships)
                    .Include(user => user.GroupMemberships).FirstOrDefault();

                if (currentUser != null)
                {
                    User copy = (User)currentUser.ShallowCopy();
                    copy.OrganizationMemberships = currentUser.OrganizationMemberships.Where(om => !om.Archived).ToList();
                    copy.GroupMemberships = currentUser.GroupMemberships.Where(gm => !gm.Archived).ToList();
                    curUser = copy;
                }
            }
            return curUser;
        }
        public bool IsSuperAdmin(User currentuser)
        {
            return currentuser.HasOrgRole(RoleName.SuperAdmin, 0);
        }
        public bool IsOrgAdmin(User currentuser, int orgId)
        {
            return currentuser.HasOrgRole(RoleName.Admin, orgId);
        }
    }
}