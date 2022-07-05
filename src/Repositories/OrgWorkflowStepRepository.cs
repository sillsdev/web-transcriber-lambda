using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class OrgWorkflowStepRepository : BaseRepository<Orgworkflowstep>
    {
        private readonly OrganizationRepository OrganizationRepository;

        public OrgWorkflowStepRepository(
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

        public IQueryable<Orgworkflowstep> UsersOrgWorkflowSteps(
            IQueryable<Orgworkflowstep> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities.Where(om => orgIds.Contains(om.OrganizationId));
            }
            return entities;
        }

        public IQueryable<Orgworkflowstep> ProjectOrgWorkflowSteps(
            IQueryable<Orgworkflowstep> entities,
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
        public override IQueryable<Orgworkflowstep> FromProjectList(
            IQueryable<Orgworkflowstep>? entities,
            string idList
        )
        {
            return ProjectOrgWorkflowSteps(entities ?? GetAll(), idList);
        }

        public override IQueryable<Orgworkflowstep> FromCurrentUser(
            IQueryable<Orgworkflowstep>? entities = null
        )
        {
            return UsersOrgWorkflowSteps(entities ?? GetAll());
        }
        #endregion
    }
}
