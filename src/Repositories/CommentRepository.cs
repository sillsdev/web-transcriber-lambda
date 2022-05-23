using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using System.Linq;
using System.Collections.Generic;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;

namespace SIL.Transcriber.Repositories
{
    public class CommentRepository : BaseRepository<Comment>
    {
        private readonly PlanRepository PlanRepository;
        public CommentRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            PlanRepository  planRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
                loggerFactory, resourceDefinitionAccessor, currentUserRepository)
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
        public IQueryable<Comment> UsersComments(IQueryable<Comment> entities, IQueryable<Project> projects)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersComments(entities, plans);
        }
        private IQueryable<Comment> PlansComments(IQueryable<Comment> entities, IQueryable<Plan> plans)
        {
            IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Join(plans, (m => m.PlanId), p => p.Id, (m, p) => m);
            IQueryable<Discussion> discussions = dbContext.Discussions.Join(mediafiles, (d => d.MediafileId), m => m.Id, (d,m) => d);
            return entities.Join(discussions, c => c.DiscussionId, d => d.Id, (c,d) => c);
        }

        private IQueryable<Comment> UsersComments(IQueryable<Comment> entities, IQueryable<Plan>? plans = null)
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
        protected override IQueryable<Comment> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectsComments(base.GetAll(), idList);
        }
        protected override IQueryable<Comment> FromCurrentUser(QueryLayer layer)
        {
            return UsersComments(base.GetAll());
        }
        #endregion
    }
}