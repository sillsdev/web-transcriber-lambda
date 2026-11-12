using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class MediareplacementRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        PlanRepository planRepository,
        ProjectRepository projectRepository
        ) : BaseRepository<Mediareplacement>(
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
        readonly private PlanRepository PlanRepository = planRepository;
        readonly private ProjectRepository ProjectRepository = projectRepository;
        private static IQueryable<Mediareplacement> PlansMediareplacements(IQueryable<Mediareplacement> entities, IQueryable<Plan> plans)
        {
            return entities.Join(plans, m => m.PlanId, p => p.Id, (m, p) => m);
        }

        private IQueryable<Mediareplacement> UsersMediareplacements(IQueryable<Mediareplacement> entities, IQueryable<Plan>? plans = null)
        {
            plans ??= PlanRepository.UsersPlans(dbContext.Plans);

            return PlansMediareplacements(entities, plans);
        }

        public IQueryable<Mediareplacement> ProjectsMediareplacements(IQueryable<Mediareplacement> entities, string idlist)
        {
            IQueryable<Project> projects = ProjectRepository.FromIdList(dbContext.Projects, idlist);
            IQueryable<Plan> plans = PlanRepository.ProjectPlans(dbContext.Plans, projects);
            return PlansMediareplacements(entities, plans);
        }
        #region Overrides
        public override IQueryable<Mediareplacement> FromProjectList(
            IQueryable<Mediareplacement>? entities,
            string idList
        )
        {
            return ProjectsMediareplacements(entities ?? GetAll(), idList);
        }

        public override IQueryable<Mediareplacement> FromCurrentUser(
            IQueryable<Mediareplacement>? entities = null
        )
        {
            return UsersMediareplacements(entities ?? GetAll());
        }
        #endregion
    }
}
