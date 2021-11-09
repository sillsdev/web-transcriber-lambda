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
    public class OrgArtifactTypeRepository : BaseRepository<OrgArtifactType>
    {
        private OrganizationRepository OrganizationRepository;
        public OrgArtifactTypeRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<OrgArtifactType> UsersOrgArtifactTypes(IQueryable<OrgArtifactType> entities)
        {
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities
                       .Where(om => orgIds.Contains(om.OrganizationId));
            }
            return entities;
        }
        public IQueryable<OrgArtifactType> ProjectOrgArtifactTypes(IQueryable<OrgArtifactType> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region Overrides
        public override IQueryable<OrgArtifactType> Filter(IQueryable<OrgArtifactType> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    bool hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersOrgArtifactTypes(entities).Where(om => om.Id == specifiedOrgId);
                }
                return UsersOrgArtifactTypes(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersOrgArtifactTypes(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectOrgArtifactTypes(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
