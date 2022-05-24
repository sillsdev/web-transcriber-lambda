using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Repositories
{
    public class ArtifactCategoryRepository : BaseRepository<Artifactcategory>
    {
        private OrganizationRepository OrganizationRepository;
        public ArtifactCategoryRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
                loggerFactory,resourceDefinitionAccessor, currentUserRepository)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<Artifactcategory> UsersArtifactCategorys(IQueryable<Artifactcategory> entities)
        {
            if (CurrentUser == null) return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty() ?? new List<int>();
                entities = entities
                       .Where(om => om.OrganizationId == null || orgIds.Contains((int)om.OrganizationId));
            }
            return entities;
        }
        public IQueryable<Artifactcategory> ProjectArtifactCategorys(IQueryable<Artifactcategory> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            IQueryable<int> ids = orgs.Select(o => o.Id);
            return entities.Where(om => om.OrganizationId == null || ids.Contains((int)om.OrganizationId));
        }

        #region Overrides
        protected override IQueryable<Artifactcategory> FromProjectList(IQueryable<Artifactcategory>? entities, string idList)
        {
            return ProjectArtifactCategorys(entities??GetAll(), idList);
        }
        public override IQueryable<Artifactcategory> FromCurrentUser(IQueryable<Artifactcategory>? entities=null)
        {
            return UsersArtifactCategorys(entities??GetAll());
        }
        #endregion
    }
}
