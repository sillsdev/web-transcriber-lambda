using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class OrgKeytermRepository : BaseRepository<Orgkeyterm>
    {
        private readonly OrganizationRepository OrganizationRepository;

        public OrgKeytermRepository(
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

        public IQueryable<Orgkeyterm> UsersOrgKeyterms(
            IQueryable<Orgkeyterm> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities.Where(om =>      orgIds.Contains(om.OrganizationId));
            }
            return entities;
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
