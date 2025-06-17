using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class ArtifactCategoryRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Artifactcategory>(
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
        private readonly OrganizationRepository OrganizationRepository = organizationRepository;


        public Bible? GetBible(Artifactcategory resource)
        {

            int orgId = resource.OrganizationId ?? resource.Organization?.Id ?? 0;
            return dbContext.Bibles.Join(dbContext.Organizationbibles.Where(o => o.OrganizationId == orgId && !o.Archived), b => b.Id, o => o.BibleId, (b, o) => b).Where(b => !b.Archived).FirstOrDefault();

        }

        public IQueryable<Artifactcategory> UsersArtifactCategorys(
            IQueryable<Artifactcategory> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty() ?? new List<int>();
                entities = entities.Where(
                    om => (om.OrganizationId == null || orgIds.Contains((int)om.OrganizationId))
                );
            }
            return entities;
        }

        public IQueryable<Artifactcategory> ProjectArtifactCategorys(
            IQueryable<Artifactcategory> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            IQueryable<int> ids = orgs.Select(o => o.Id);
            return entities.Where(
                om => (om.OrganizationId == null || ids.Contains((int)om.OrganizationId))
            );
        }

        #region Overrides
        public override IQueryable<Artifactcategory> FromProjectList(
            IQueryable<Artifactcategory>? entities,
            string idList
        )
        {
            return ProjectArtifactCategorys(entities ?? GetAll(), idList);
        }

        public override IQueryable<Artifactcategory> FromCurrentUser(
            IQueryable<Artifactcategory>? entities = null
        )
        {
            return UsersArtifactCategorys(entities ?? GetAll());
        }
        #endregion
    }
}
