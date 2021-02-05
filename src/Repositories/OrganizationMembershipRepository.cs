using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Data;
using System.Collections.Generic;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationMembershipRepository : BaseRepository<OrganizationMembership>
    {
        private OrganizationRepository OrganizationRepository;
        public OrganizationMembershipRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<OrganizationMembership> UsersOrganizationMemberships(IQueryable<OrganizationMembership> entities)
        {
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all oms in that org
                //otherwise give me just the oms I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(o => CurrentUser.HasOrgRole(RoleName.Admin, o));
                entities = entities
                       .Where(om => orgadmins.Contains(om.OrganizationId) || om.UserId == CurrentUser.Id);
            }
            return entities;
        }
        public IQueryable<OrganizationMembership> ProjectOrganizationMemberships(IQueryable<OrganizationMembership> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
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
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectOrganizationMemberships(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
