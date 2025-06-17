using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class DiscussionRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        PlanRepository planRepository
        ) : BaseRepository<Discussion>(
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
        private readonly PlanRepository PlanRepository = planRepository;

        public IQueryable<Discussion> UsersDiscussions(IQueryable<Discussion> entities, int project)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == project);
            return UsersDiscussions(entities, projects);
        }

        //get my Discussions in these projects
        public IQueryable<Discussion> UsersDiscussions(
            IQueryable<Discussion> entities,
            IQueryable<Project> projects
        )
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersDiscussions(entities, plans);
        }

        private IQueryable<Discussion> PlansDiscussions(
            IQueryable<Discussion> entities,
            IQueryable<Plan> plans
        )
        {
            IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Join(
                plans,
                (m => m.PlanId),
                p => p.Id,
                (m, p) => m
            );
            return entities.Join(
                mediafiles,
                (d => d.MediafileId),
                m => m.Id,
                (d, m) => d
            );
        }

        private IQueryable<Discussion> UsersDiscussions(
            IQueryable<Discussion> entities,
            IQueryable<Plan>? plans = null
        )
        {
            plans ??= PlanRepository.UsersPlans(dbContext.Plans);
            return PlansDiscussions(entities, plans);
        }

        private IQueryable<Discussion> ProjectsDiscussions(
            IQueryable<Discussion> entities,
            string projectid
        )
        {
            IQueryable<Plan> plans = PlanRepository.ProjectPlans(dbContext.Plans, projectid);
            return PlansDiscussions(entities, plans);
        }

        #region overrides
        public override IQueryable<Discussion> FromProjectList(
            IQueryable<Discussion>? entities,
            string idList
        )
        {
            return ProjectsDiscussions(entities ?? GetAll(), idList);
        }

        public override IQueryable<Discussion> FromCurrentUser(
            IQueryable<Discussion>? entities = null
        )
        {
            return UsersDiscussions(entities ?? GetAll());
        }
        #endregion
    }
}
