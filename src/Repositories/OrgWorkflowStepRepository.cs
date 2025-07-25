using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class OrgWorkflowStepRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Orgworkflowstep>(
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

        public IQueryable<Orgworkflowstep> UsersOrgWorkflowSteps(
            IQueryable<Orgworkflowstep> entities
        )
        {
            return CurrentUser == null
                ? entities.Where(e => e.Id == -1)
                : entities.Where(om => CurrentUser.OrganizationIds.Contains(om.OrganizationId));
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
