using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System.Linq;
using SIL.Transcriber.Data;
using System.Collections.Generic;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;

namespace SIL.Transcriber.Repositories
{
    public class ArtifactTypeRepository : BaseRepository<Artifacttype>
    {
        private OrganizationRepository OrganizationRepository;
        public ArtifactTypeRepository(
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
        public IQueryable<Artifacttype> UsersArtifactTypes(IQueryable<Artifacttype> entities)
        {
            if (CurrentUser == null) return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                entities = entities
                             .Where(om => om.OrganizationId == null || orgIds.Contains((int)om.OrganizationId));
            }
            return entities;
        }
        public IQueryable<Artifacttype> ProjectArtifactTypes(IQueryable<Artifacttype> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            IQueryable<int> ids = orgs.Select(o => o.Id);
            return entities.Where(om => om.OrganizationId == null || ids.Contains((int)om.OrganizationId));
        }

        #region Overrides
        protected override IQueryable<Artifacttype> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectArtifactTypes(base.GetAll(), idList);
        }
        protected override IQueryable<Artifacttype> FromCurrentUser(QueryLayer layer)
        {
            return UsersArtifactTypes(base.GetAll());
        }
        #endregion
    }
}
