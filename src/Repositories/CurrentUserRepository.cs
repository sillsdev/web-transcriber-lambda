using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.EnvironmentHelpers;

namespace SIL.Transcriber.Repositories
{
    public class CurrentUserRepository : EntityFrameworkCoreRepository<CurrentUser, int>
    {
        // NOTE: this repository MUST not rely on any other repositories or services
        protected readonly AppDbContext dbContext;

        public CurrentUserRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            ICurrentUserContext currentUserContext
       ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
           loggerFactory, resourceDefinitionAccessor)
        {
            dbContext = (AppDbContext)contextResolver.GetContext();
            CurrentUserContext = currentUserContext;
            Logger = loggerFactory.CreateLogger<User>();
        }

        //private AppDbContext DBContext { get; }
        private ICurrentUserContext CurrentUserContext { get; }
        protected ILogger<User> Logger { get; set; }
        private User? curUser;
        // memoize once per local thread,
        // since the current user can't change in a single request
        // this should be ok.
        public User? GetCurrentUser()
        {
            if (curUser == null)
            {
                string auth0Id = GetVarOrDefault("SIL_TR_DEBUGUSER", CurrentUserContext.Auth0Id);

                curUser = dbContext.Users
                    .Where(user => !user.Archived && (user.ExternalId ?? "").Equals(auth0Id))
                    .Include(user => user.OrganizationMemberships.Where(om => !om.Archived))
                    .Include(user => user.GroupMemberships.Where(gm => !gm.Archived))
                    .FirstOrDefault();

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

        public CurrentUser? Get()
        {
            string auth0Id = GetVarOrDefault("SIL_TR_DEBUGUSER", this.CurrentUserContext.Auth0Id);
            User? user= dbContext.Users
                     .Where(user => !user.Archived && (user.ExternalId??"").Equals(auth0Id)).FirstOrDefault();
            if (user == null)
                return null;
            CurrentUser cu = new(user)
            {
                LastModifiedByUser = null
            };
            return cu;
        }
    }
}