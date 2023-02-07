using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class OrgKeytermTargetRepository : BaseRepository<Orgkeytermtarget>
    {
        private readonly OrganizationRepository OrganizationRepository;

        public OrgKeytermTargetRepository(
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

        public IQueryable<Orgkeytermtarget> UsersOrgKeytermTargets(
            IQueryable<Orgkeytermtarget> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities.Where(om => !om.Archived && orgIds.Contains(om.OrganizationId));
            }
            return entities.Where(e => !e.Archived);
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
