using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class CommentRepository : BaseRepository<Comment>
    {
        private readonly PlanRepository PlanRepository;

        public CommentRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            PlanRepository planRepository
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
            PlanRepository = planRepository;
        }

        public IQueryable<Comment> UsersComments(IQueryable<Comment> entities, int project)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == project);
            return UsersComments(entities, projects);
        }

        //get my Comments in these projects
        public IQueryable<Comment> UsersComments(
            IQueryable<Comment> entities,
            IQueryable<Project> projects
        )
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersComments(entities, plans);
        }

        private IQueryable<Comment> PlansComments(
            IQueryable<Comment> entities,
            IQueryable<Plan> plans
        )
        {
            IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Join(
                plans,
                (m => m.PlanId),
                p => p.Id,
                (m, p) => m
            );
            IQueryable<Discussion> discussions = dbContext.Discussions.Join(
                mediafiles,
                (d => d.MediafileId),
                m => m.Id,
                (d, m) => d
            );
            return entities.Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c);
        }

        private IQueryable<Comment> UsersComments(
            IQueryable<Comment> entities,
            IQueryable<Plan>? plans = null
        )
        {
            if (plans == null)
                plans = PlanRepository.UsersPlans(dbContext.Plans);

            return PlansComments(entities, plans);
        }

        private IQueryable<Comment> ProjectsComments(IQueryable<Comment> entities, string projectid)
        {
            IQueryable<Plan> plans = PlanRepository.ProjectPlans(dbContext.Plans, projectid);
            return PlansComments(entities, plans);
        }

        #region overrides
        public override IQueryable<Comment> FromProjectList(
            IQueryable<Comment>? entities,
            string idList
        )
        {
            return ProjectsComments(entities ?? GetAll(), idList);
        }

        public override IQueryable<Comment> FromCurrentUser(IQueryable<Comment>? entities = null)
        {
            return UsersComments(entities ?? GetAll());
        }
        #endregion
    }
}
