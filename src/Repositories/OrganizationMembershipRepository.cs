using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Linq;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationMembershipRepository : BaseRepository<OrganizationMembership>
    {
        public OrganizationMembershipRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        public IQueryable<OrganizationMembership> UsersOrganizationMemberships(IQueryable<OrganizationMembership> entities)
        {
            var orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all oms in that org
                //otherwise give me just the oms I'm a member of
                var orgadmins = orgIds.Where(o => CurrentUser.HasOrgRole(RoleName.Admin, o));
                entities = entities
                       .Where(om => orgadmins.Contains(om.OrganizationId) || om.UserId == CurrentUser.Id);
            }
            return entities;
        }
        #region Overrides
        public override IQueryable<OrganizationMembership> Filter(IQueryable<OrganizationMembership> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    bool hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersOrganizationMemberships(entities).Where(om => om.Id == specifiedOrgId) ;
                }
                return UsersOrganizationMemberships(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersOrganizationMemberships(entities);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
