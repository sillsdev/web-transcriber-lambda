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
using static SIL.Transcriber.Utility.RepositoryExtensions;
using SIL.Transcriber.Utility;

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

        public override IQueryable<User> Filter(IQueryable<User> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Attribute.Equals("name", StringComparison.OrdinalIgnoreCase)) {
                return entities.Where(u => EFUtils.Like(u.Name, filterQuery.Value));
            }
            return  entities.OptionallyFilterOnQueryParam(filterQuery,
                                          "organization-id",
                                          this,
                                          CurrentUserContext,
                                          GetWithOrganizationId,
                                          FilterOnOrganizationHeader,
                                          null,
                                          null);

        }
        protected IQueryable<User> FilterOnOrganizationHeader(IQueryable<User>query, FilterQuery filterQuery)
        {
            return query.OptionallyFilterOnQueryParam(filterQuery,
                                          "organization-header",
                                          this,
                                          CurrentUserContext,
                                          GetWithFilter,
                                          base.Filter,
                                         GetAllUsersByCurrentUser,
                                         null);

        }
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
                       .Where(e => e.ExternalId == auth0Id)
                       .Include(user => user.OrganizationMemberships)
                       .Include(user => user.GroupMemberships)
                       .FirstOrDefaultAsync();
        }
    }
}
