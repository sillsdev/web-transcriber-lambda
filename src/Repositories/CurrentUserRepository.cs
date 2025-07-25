using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using static SIL.Transcriber.Utility.HttpContextHelpers;

namespace SIL.Transcriber.Repositories
{
    public class CurrentUserRepository(
                IHttpContextAccessor httpContextAccessor,
        ITargetedFields targetedFields, AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph, IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        ICurrentUserContext currentUserContext
       ) : EntityFrameworkCoreRepository<CurrentUser, int>(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
       loggerFactory, resourceDefinitionAccessor)
    {
        // NOTE: this repository MUST not rely on any other repositories or services
        protected readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();
        readonly private HttpContext? HttpContext = httpContextAccessor.HttpContext;
        //private AppDbContext DBContext { get; }
        private ICurrentUserContext CurrentUserContext { get; } = currentUserContext;
        protected ILogger<User> Logger { get; set; } = loggerFactory.CreateLogger<User>();
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
                    .Include(user => user.GroupMemberships.Where(gm => !gm.Archived)).FirstOrDefault();

                if (curUser != null)
                {
                    IQueryable<User> dupUsers = dbContext.Users.Where(user => !user.Archived && (user.Email ?? "").Equals(curUser.Email)).OrderBy(user => user.Id);
                    curUser = dupUsers.FirstOrDefault();
                    if (curUser != null && dupUsers.Count() > 1)
                    {
                        bool changed = false;
                        if (!(curUser.ExternalId ?? "").StartsWith("google"))
                        {
                            User? googleuser = dupUsers.Where(u => (u.ExternalId??"").StartsWith("google")).OrderByDescending(u => u.DateCreated).FirstOrDefault();
                            if (googleuser != null)
                            {
                                curUser.AvatarUrl = googleuser.AvatarUrl;
                                changed = true;
                            }
                        }
                        IEnumerable<Organizationmembership> oms = [.. dbContext.Organizationmemberships.Where(x => !x.Archived).Join(dupUsers, gm => gm.UserId, u => u.Id, (gm, u) => gm)];
                        foreach (Organizationmembership om in oms.Where(gm => gm.UserId != curUser.Id))
                        {
                            if (!oms.Any(x => x.UserId == curUser.Id && x.OrganizationId == om.OrganizationId))
                            {
                                om.UserId = curUser.Id;

                                dbContext.Update(om);
                                changed = true;
                            }
                        }
                        IEnumerable<Groupmembership> gms = [.. dbContext.Groupmemberships.Where(x => !x.Archived).Join(dupUsers, gm => gm.UserId, u => u.Id, (gm, u) => gm)];
                        foreach (Groupmembership gm in gms.Where(gm => gm.UserId != curUser.Id))
                        {
                            if (!gms.Any(x => x.UserId == curUser.Id && x.GroupId == gm.GroupId))
                            {
                                gm.UserId = curUser.Id;
                                dbContext.Update(gm);
                                changed = true;
                            }
                        }
                        if (changed)
                        {
                            HttpContext?.SetFP("dupuser");
                            dbContext.Update(curUser); //I want to update the date if anything changed
                            dbContext.SaveChanges();
                        }
                    }
                }
            }
            return curUser;
        }
        /* not used
        public bool IsOrgAdmin(User currentuser, int orgId)
        {
            return currentuser.HasOrgRole(RoleName.Admin, orgId);
        }
        */
        public CurrentUser? Get()
        {
            User? user= GetCurrentUser();
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