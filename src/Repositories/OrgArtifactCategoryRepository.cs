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
    public class OrgArtifactCategoryRepository : BaseRepository<OrgArtifactCategory>
    {
        private OrganizationRepository OrganizationRepository;
        public OrgArtifactCategoryRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<OrgArtifactCategory> UsersOrgArtifactCategorys(IQueryable<OrgArtifactCategory> entities)
        {
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities
                       .Where(om => orgIds.Contains(om.OrganizationId));
            }
            return entities;
        }
        public IQueryable<OrgArtifactCategory> ProjectOrgArtifactCategorys(IQueryable<OrgArtifactCategory> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region Overrides
        public override IQueryable<OrgArtifactCategory> Filter(IQueryable<OrgArtifactCategory> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    bool hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersOrgArtifactCategorys(entities).Where(om => om.Id == specifiedOrgId);
                }
                return UsersOrgArtifactCategorys(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersOrgArtifactCategorys(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectOrgArtifactCategorys(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
