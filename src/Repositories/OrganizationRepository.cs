using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationRepository : BaseRepository<Organization>
    {
        public OrganizationRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        public IQueryable<Organization> UsersOrganizations(IQueryable<Organization> entities)
        {
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                return entities.Join(dbContext.Organizationmemberships.Where(om => om.UserId == CurrentUser.Id && !om.Archived), o => o.Id, om => om.OrganizationId, (o, om) => o).GroupBy(o => o.Id).Select(g => g.First());
            }
            return entities;
        }
        #region Overrides
        public override IQueryable<Organization> Filter(IQueryable<Organization> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    var hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersOrganizations(entities).Where(om => om.Id == specifiedOrgId) ;
                }
                return UsersOrganizations(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersOrganizations(entities);
            }
            if (filterQuery.Has(DATA_START_INDEX)) //ignore
            {
                return entities;
            }
            if (filterQuery.Has(PROJECT_SEARCH_TERM)) //ignore
            {
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
