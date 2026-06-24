using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Paratext.Models;
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
                        IEnumerable<Organizationmembership> oms = [.. dbContext.Organizationmemberships.Join(dupUsers, gm => gm.UserId, u => u.Id, (gm, u) => gm).Join(dbContext.Organizations.Where(o => !o.Archived), om => om.OrganizationId, o => o.Id, (om, o) => om)];
                        foreach (Organizationmembership om in oms.Where(gm => gm.UserId != curUser.Id))
                        {
                            Organizationmembership? tmp = oms.Where(x => x.UserId == curUser.Id && x.OrganizationId == om.OrganizationId).FirstOrDefault();
                            if (tmp != null)
                            {
                                if (tmp.Archived)
                                {
                                    tmp.Archived = false;
                                    dbContext.Update(tmp);
                                    changed = true;
                                }
                            }
                            else
                            {
                                om.UserId = curUser.Id;

                                dbContext.Update(om);
                                changed = true;
                            }
                        }
                        IEnumerable<Groupmembership> gms = [.. dbContext.Groupmemberships.Join(dupUsers, gm => gm.UserId, u => u.Id, (gm, u) => gm).Join(dbContext.Groups.Where(o => !o.Archived), om => om.GroupId, o => o.Id, (om, o) => om)];
                        foreach (Groupmembership gm in gms.Where(gm => gm.UserId != curUser.Id))
                        {
                            Groupmembership? tmp = gms.Where(x => x.UserId == curUser.Id && x.GroupId == gm.GroupId).FirstOrDefault();
                            if (tmp != null)
                            {
                                if (tmp.Archived)
                                {
                                    tmp.Archived = false;
                                    dbContext.Update(tmp);
                                    changed = true;
                                }
                            }
                            else
                            {
                                gm.UserId = curUser.Id;
                                dbContext.Update(gm);
                                changed = true;
                            }
                        }
                        IEnumerable<ParatextToken> pts = [.. dbContext.Paratexttokens.Join(dupUsers, pt => pt.UserId, u => u.Id, (pt, u) => pt)];
                        foreach (ParatextToken pt in pts.Where(x => x.UserId != curUser.Id))
                        {
                            if (!pts.Any(x => x.UserId == curUser.Id))
                            {
                                pt.UserId = curUser.Id;
                                dbContext.Update(pt);
                                changed = true;
                            }
                        }
                        if (!(curUser.SharedContentCreator ?? false) && dupUsers.Any(u => u.SharedContentCreator ?? false))
                        {
                            curUser.SharedContentCreator = true;
                            changed = true;
                        }
                        if (!(curUser.SharedContentAdmin ?? false) && dupUsers.Any(u => u.SharedContentAdmin ?? false))
                        {
                            curUser.SharedContentAdmin = true;
                            changed = true;
                        }
                        if (!(curUser.CanPublish ?? false) && dupUsers.Any(u => u.CanPublish ?? false))
                        {
                            curUser.CanPublish = true;
                            changed = true;
                        }
                        if (changed)
                        {
                            string fp = HttpContext?.GetFP() ?? "";
                            HttpContext?.SetFP("dupuser");
                            dbContext.Update(curUser); //I want to update the date if anything changed
                            dbContext.SaveChanges();
                            HttpContext?.SetFP(fp);
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