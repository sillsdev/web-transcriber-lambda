using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class ArtifactTypeRepository : BaseRepository<Artifacttype>
    {
        private readonly OrganizationRepository OrganizationRepository;

        public ArtifactTypeRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository
        )
            : base(
                targetedFields,
                contextResolver,
                resourceGraph,
                resourceFactory,
                constraintProviders,
                loggerFactory,
                resourceDefinitionAccessor,
                currentUserRepository
            )
        {
            OrganizationRepository = organizationRepository;
        }

        public IQueryable<Artifacttype> UsersArtifactTypes(IQueryable<Artifacttype> entities)
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                entities = entities.Where(
                    om => !om.Archived && (om.OrganizationId == null || orgIds.Contains((int)om.OrganizationId))
                );
            }
            return entities.Where(om=>!om.Archived);
        }

        public IQueryable<Artifacttype> ProjectArtifactTypes(
            IQueryable<Artifacttype> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            IQueryable<int> ids = orgs.Select(o => o.Id);
            return entities.Where(
                om => !om.Archived && (om.OrganizationId == null || ids.Contains((int)om.OrganizationId))
            );
        }

        #region Overrides
        public override IQueryable<Artifacttype> FromProjectList(
            IQueryable<Artifacttype>? entities,
            string idList
        )
        {
            return ProjectArtifactTypes(entities ?? GetAll(), idList);
        }

        public override IQueryable<Artifacttype> FromCurrentUser(
            IQueryable<Artifacttype>? entities = null
        )
        {
            return UsersArtifactTypes(entities ?? GetAll());
        }
        #endregion
    }
}
