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
    public class ArtifactTypeRepository : BaseRepository<ArtifactType>
    {
        private OrganizationRepository OrganizationRepository;
        public ArtifactTypeRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<ArtifactType> UsersArtifactTypes(IQueryable<ArtifactType> entities)
        {
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities
                             .Where(om => om.OrganizationId == null || orgIds.Contains((int)om.OrganizationId));
            }
            return entities;
        }
        public IQueryable<ArtifactType> ProjectArtifactTypes(IQueryable<ArtifactType> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            IQueryable<int> ids = orgs.Select(o => o.Id);
            return entities.Where(om => om.OrganizationId == null || ids.Contains((int)om.OrganizationId));
        }

        #region Overrides
        public override IQueryable<ArtifactType> Filter(IQueryable<ArtifactType> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    int specifiedOrgId;
                    bool hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out specifiedOrgId);
                    return UsersArtifactTypes(entities).Where(om => om.Id == specifiedOrgId);
                }
                return UsersArtifactTypes(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersArtifactTypes(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectArtifactTypes(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
