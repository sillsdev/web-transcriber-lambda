using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class OrgKeytermRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Orgkeyterm>(
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

        public IQueryable<Orgkeyterm> UsersOrgKeyterms(
            IQueryable<Orgkeyterm> entities
        )
        {
            return CurrentUser == null
                ? entities.Where(e => e.Id == -1)
                : entities.Where(om => CurrentUser.OrganizationIds.Contains(om.OrganizationId));
        }

        public IQueryable<Orgkeyterm> ProjectOrgKeyterms(
            IQueryable<Orgkeyterm> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region Overrides
        public override IQueryable<Orgkeyterm> FromProjectList(
            IQueryable<Orgkeyterm>? entities,
            string idList
        )
        {
            return ProjectOrgKeyterms(entities ?? GetAll(), idList);
        }

        public override IQueryable<Orgkeyterm> FromCurrentUser(
            IQueryable<Orgkeyterm>? entities = null
        )
        {
            return UsersOrgKeyterms(entities ?? GetAll());
        }
        #endregion
    }
}
