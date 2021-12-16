﻿using JsonApiDotNetCore.Services;
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
    public class CommentRepository : BaseRepository<Comment>
    {
        private PlanRepository PlanRepository;
        public CommentRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          AppDbContextResolver contextResolver,
          PlanRepository planRepository
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
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

        private IQueryable<Comment> UsersComments(IQueryable<Comment> entities, IQueryable<Plan> plans = null)
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
        public override IQueryable<Comment> Filter(IQueryable<Comment> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersComments(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectsComments(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
    }
}