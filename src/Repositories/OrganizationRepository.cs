using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationRepository : BaseRepository<Organization>
    {
        public OrganizationRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        public override IQueryable<Organization> Filter(IQueryable<Organization> entities, FilterQuery filterQuery)
        {
            var attribute = filterQuery.Attribute;
            var value = filterQuery.Value;
            var isScopeToUser = attribute.Equals("scope-to-current-user", StringComparison.OrdinalIgnoreCase);

            if (isScopeToUser) {
                var orgIds = CurrentUser.OrganizationIds.OrEmpty();

                var scopedToUser = entities.Where(organization => orgIds.Contains(organization.Id));

                // return base.Filter(scopedToUser, filterQuery);
                return scopedToUser;
            }

            return base.Filter(entities, filterQuery);
        }
    }
}
