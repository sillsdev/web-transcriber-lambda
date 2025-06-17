using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationSchemeRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Organizationscheme>(
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
        readonly private OrganizationRepository OrganizationRepository = organizationRepository;

        public IQueryable<Organizationscheme> UsersOrganizationSchemes(
            IQueryable<Organizationscheme> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                entities = entities.Where(
                    os => (orgIds.Contains(os.OrganizationId))
                );
            }
            return entities;
        }

        public IQueryable<Organizationscheme> ProjectOrganizationSchemes(
            IQueryable<Organizationscheme> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region overrides
        public override IQueryable<Organizationscheme> FromCurrentUser(
            IQueryable<Organizationscheme>? entities = null
        )
        {
            return UsersOrganizationSchemes(entities ?? GetAll());
        }

        public override IQueryable<Organizationscheme> FromProjectList(
            IQueryable<Organizationscheme>? entities,
            string idList
        )
        {
            return ProjectOrganizationSchemes(entities ?? GetAll(), idList);
        }
        #endregion
    }
}
