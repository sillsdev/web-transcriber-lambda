using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class OrgKeytermTargetRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Orgkeytermtarget>(
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

        public IQueryable<Orgkeytermtarget> UsersOrgKeytermTargets(
            IQueryable<Orgkeytermtarget> entities
        )
        {
            return CurrentUser == null
                ? entities.Where(e => e.Id == -1)
                : entities.Where(om => CurrentUser.OrganizationIds.Contains(om.OrganizationId));
        }

        public IQueryable<Orgkeytermtarget> ProjectOrgKeytermTargets(
            IQueryable<Orgkeytermtarget> entities,
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
        public override IQueryable<Orgkeytermtarget> FromProjectList(
            IQueryable<Orgkeytermtarget>? entities,
            string idList
        )
        {
            return ProjectOrgKeytermTargets(entities ?? GetAll(), idList);
        }

        public override IQueryable<Orgkeytermtarget> FromCurrentUser(
            IQueryable<Orgkeytermtarget>? entities = null
        )
        {
            return UsersOrgKeytermTargets(entities ?? GetAll());
        }
        #endregion
    }
}
