using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;

namespace SIL.Transcriber.Repositories
{
    public class OrgWorkflowStepRepository : BaseRepository<OrgWorkflowstep>
    {
        private readonly OrganizationRepository OrganizationRepository;
        public OrgWorkflowStepRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
                loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<OrgWorkflowstep> UsersOrgWorkflowSteps(IQueryable<OrgWorkflowstep> entities)
        {
            if (CurrentUser == null) return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities
                       .Where(om => orgIds.Contains(om.OrganizationId));
            }
            return entities;
        }
        public IQueryable<OrgWorkflowstep> ProjectOrgWorkflowSteps(IQueryable<OrgWorkflowstep> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region Overrides
        protected override IQueryable<OrgWorkflowstep> FromProjectList(IQueryable<OrgWorkflowstep>? entities, string idList)
        {
            return ProjectOrgWorkflowSteps(entities??GetAll(), idList);
        }
        public override IQueryable<OrgWorkflowstep> FromCurrentUser(IQueryable<OrgWorkflowstep>? entities = null)
        {
            return UsersOrgWorkflowSteps(entities ?? GetAll());
        }
        #endregion
    }
}
