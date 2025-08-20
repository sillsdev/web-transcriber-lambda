using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationMembershipRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Organizationmembership>(
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

        public IQueryable<Organizationmembership> UsersOrganizationMemberships(
            IQueryable<Organizationmembership> entities
        )
        {
            return CurrentUser == null
                ? entities.Where(e => e.Id == -1)
                : entities.Where(
                om => (CurrentUser.OrganizationIds.Contains(om.OrganizationId))
            );
        }

        public IQueryable<Organizationmembership> ProjectOrganizationMemberships(
            IQueryable<Organizationmembership> entities,
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
        public override IQueryable<Organizationmembership> FromCurrentUser(
            IQueryable<Organizationmembership>? entities = null
        )
        {
            return UsersOrganizationMemberships(entities ?? GetAll());
        }

        public override IQueryable<Organizationmembership> FromProjectList(
            IQueryable<Organizationmembership>? entities,
            string idList
        )
        {
            return ProjectOrganizationMemberships(entities ?? GetAll(), idList);
        }
        #endregion
    }
}
