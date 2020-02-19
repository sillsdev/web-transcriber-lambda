using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
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

namespace SIL.Transcriber.Repositories
{
    public class UserRepository : BaseRepository<User>
    {
        public ICurrentUserContext CurrentUserContext { get; }
        public UserRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository,  contextResolver)
        {
            this.CurrentUserContext = currentUserContext;
        }
        public IQueryable<User> UsersUsers(IQueryable<User> entities, int org = 0)
        {
            if (org != 0)
            {
                return entities.Join(dbContext.Organizationmemberships.Where(om => om.OrganizationId==org), u => u.Id, om => om.UserId, (u, om) => u).GroupBy(u => u.Id).Select(g => g.First());
            }
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                var orgIds = CurrentUser.OrganizationIds.OrEmpty();
                //always give me all users in that org
                //add groupby and select because once we join with om, we duplicate users
                entities = entities.Join(dbContext.Organizationmemberships.Where(om => orgIds.Contains(om.OrganizationId)), u => u.Id, om => om.UserId, (u, om) => u).GroupBy(u => u.Id).Select(g => g.First());
            }
            return entities;
        }
        #region Overrides
        public override IQueryable<User> Filter(IQueryable<User> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    var hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersUsers(entities, specifiedOrgId);
                }
                return UsersUsers(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersUsers(entities);
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
                .Where(u => u.OrganizationMemberships
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
                     .Any(om => om.OrganizationId.ToString() == value));
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
