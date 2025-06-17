using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationSchemeStepRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationSchemeRepository orgSchemeRepository
        ) : BaseRepository<Organizationschemestep>(
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
        private readonly OrganizationSchemeRepository OrgSchemeRepository = orgSchemeRepository;

        public IQueryable<Organizationschemestep> UsersOrganizationSchemeSteps(
            IQueryable<Organizationschemestep> entities
        )
        {
            IQueryable<Organizationscheme>? terms = OrgSchemeRepository.UsersOrganizationSchemes(dbContext.Organizationschemes.AsQueryable());
            return entities.Join(terms, o => o.OrganizationschemeId, r => r.Id, (o, r) => o);
        }

        public IQueryable<Organizationschemestep> ProjectOrganizationSchemeSteps(
            IQueryable<Organizationschemestep> entities,
            string projectid
        )
        {
            IQueryable<Organizationscheme>? terms = OrgSchemeRepository.ProjectOrganizationSchemes(dbContext.Organizationschemes.AsQueryable(), projectid);
            return entities.Join(terms, o => o.OrganizationschemeId, r => r.Id, (o, r) => o);
        }

        #region Overrides
        public override IQueryable<Organizationschemestep> FromProjectList(
            IQueryable<Organizationschemestep>? entities,
            string idList
        )
        {
            return ProjectOrganizationSchemeSteps(entities ?? GetAll(), idList);
        }

        public override IQueryable<Organizationschemestep> FromCurrentUser(
            IQueryable<Organizationschemestep>? entities = null
        )
        {
            return UsersOrganizationSchemeSteps(entities ?? GetAll());
        }
        #endregion
    }
}
