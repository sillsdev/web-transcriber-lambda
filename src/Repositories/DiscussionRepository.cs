using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using System.Linq;
using System.Collections.Generic;
using SIL.Transcriber.Utility;
using JsonApiDotNetCore.Internal.Query;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class DiscussionRepository : BaseRepository<Discussion>
    {
        private readonly PlanRepository PlanRepository;
        public DiscussionRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          AppDbContextResolver contextResolver,
          PlanRepository planRepository
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            PlanRepository = planRepository;
        }
        public IQueryable<Discussion> UsersDiscussions(IQueryable<Discussion> entities, int project)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == project);
            return UsersDiscussions(entities, projects);
        }
        //get my Discussions in these projects
        public IQueryable<Discussion> UsersDiscussions(IQueryable<Discussion> entities, IQueryable<Project> projects)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersDiscussions(entities, plans);
        }
        private IQueryable<Discussion> PlansDiscussions(IQueryable<Discussion> entities, IQueryable<Plan> plans)
        {
            IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Join(plans, (m => m.PlanId), p => p.Id, (m, p) => m);
            return dbContext.Discussions.Join(mediafiles, (d => d.Mediafileid), m => m.Id, (d, m) => d);
        }

        private IQueryable<Discussion> UsersDiscussions(IQueryable<Discussion> entities, IQueryable<Plan> plans = null)
        {
            if (plans == null)
                plans = PlanRepository.UsersPlans(dbContext.Plans);
            return PlansDiscussions(entities, plans);
        }
        private IQueryable<Discussion> ProjectsDiscussions(IQueryable<Discussion> entities, string projectid)
        {
            IQueryable<Plan> plans = PlanRepository.ProjectPlans(dbContext.Plans, projectid);
            return PlansDiscussions(entities, plans);
        }
        public override IQueryable<Discussion> Filter(IQueryable<Discussion> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersDiscussions(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectsDiscussions(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
    }
}