using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class UserRepository : BaseRepository<User>
    {
        public ICurrentUserContext CurrentUserContext { get; }
        private OrganizationMembershipRepository OrgMemRepository;
        public UserRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            CurrentUserRepository currentUserRepository,
            OrganizationMembershipRepository orgmemRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository,  contextResolver)
        {
            this.CurrentUserContext = currentUserContext;
            OrgMemRepository = orgmemRepository;
        }
        public IQueryable<User> OrgMemUsers(IQueryable<User> entities, IQueryable<OrganizationMembership> orgmems)
        {
            return entities.Join(orgmems, u => u.Id, om => om.UserId, (u, om) => u).GroupBy(u => u.Id).Select(g => g.First());
        }
        public IQueryable<User> UsersUsers(IQueryable<User> entities, int org = 0)
        {
            if (org != 0)
            {
                return OrgMemUsers(entities, dbContext.Organizationmemberships.Where(om => om.OrganizationId == org));
            }
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                //always give me all users in that org
                //add groupby and select because once we join with om, we duplicate users
                OrgMemUsers(entities, dbContext.Organizationmemberships.Where(om => orgIds.Contains(om.OrganizationId)));
            }
            return entities;
        }
        public IQueryable<User> ProjectUsers(IQueryable<User> entities, string projectid)
        {
            IQueryable<OrganizationMembership> orgmems = OrgMemRepository.ProjectOrganizationMemberships(dbContext.Organizationmemberships, projectid);
            return OrgMemUsers(entities, orgmems);
        }
        #region Overrides
        public override IQueryable<User> Filter(IQueryable<User> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    bool hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersUsers(entities, specifiedOrgId);
                }
                return UsersUsers(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersUsers(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectUsers(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion

        private IQueryable<User> GetAllUsersByCurrentUser(IQueryable<User> query,
                                               IEnumerable<int> orgIds)
        {
            // Get all users in the all the 
            // organizations that the current user is a member

            return query
                .Where(u => u.OrganizationMemberships.Where(om => !om.Archived)
                            .Select(o => o.OrganizationId)
                            .Intersect(orgIds)
                            .Any());
        }

        private IQueryable<User> GetWithOrganizationId(IQueryable<User> query,
               string value,
               UserRepository userRepository,
               ICurrentUserContext currentUserContext,
               Func<IQueryable<User>, IEnumerable<int>, IQueryable<User>> query1,
               Func<IQueryable<User>, IEnumerable<int>, IQueryable<User>> query2)
        {
            return query.Where(
                      u => u.OrganizationMemberships
                     .Any(om => !om.Archived && om.OrganizationId.ToString() == value));
        }

        public async Task<User> GetByAuth0Id(string auth0Id)
        {
            return await base.Get()
                       .Where(e => e.ExternalId == auth0Id && !e.Archived)
                       .Include(user => user.OrganizationMemberships)
                       .Include(user => user.GroupMemberships)
                       .FirstOrDefaultAsync();
        }
    }
}
