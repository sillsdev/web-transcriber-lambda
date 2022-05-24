﻿using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using Microsoft.EntityFrameworkCore;

namespace SIL.Transcriber.Repositories
{
    public class MediafileRepository : BaseRepository<Mediafile>
    {

        readonly private PlanRepository PlanRepository;
        readonly private ProjectRepository ProjectRepository;

        public MediafileRepository(ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
             PlanRepository planRepository,
             ProjectRepository projectRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory,
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            PlanRepository = planRepository;
            ProjectRepository = projectRepository;
        }

        public IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, int project)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id == project);
            return UsersMediafiles(entities, projects);
        }
        //get my Mediafiles in these projects
        public IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, IQueryable<Project> projects)
        {
            //this gets just the passages I have access to in these projects
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersMediafiles(entities, plans);
        }
        private IQueryable<Mediafile> PlansMediafiles(IQueryable<Mediafile> entities, IQueryable<Plan> plans)
        {
            return entities.Join(plans, m => m.PlanId, p => p.Id, (m, p) => m);
        }

        private IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, IQueryable<Plan>? plans = null)
        {
            if (plans == null)
                plans = PlanRepository.UsersPlans(dbContext.Plans);

            return PlansMediafiles(entities, plans);
        }
        private IQueryable<Mediafile> ProjectsMediafiles(IQueryable<Mediafile> entities, string idlist)
        {
            IQueryable<Project> projects = ProjectRepository.FromIdList(dbContext.Projects, idlist);
            IQueryable<Plan> plans = PlanRepository.ProjectPlans(dbContext.Plans, projects);
            return PlansMediafiles(entities, plans);
        }
        public IQueryable<Mediafile> Get()
        {
            return base.GetAll();
        }
        public Mediafile? GetLatestShared(int passageId)
        {
            return GetAll().Where(p => p.PassageId == passageId && p.ReadyToShare).OrderBy(m => m.VersionNumber).LastOrDefault();
        }
        public IQueryable<Mediafile> ReadyToSync(int PlanId, int artifactTypeId = 0)
        {
            //this should disqualify media that has a new version that isn't ready...but doesn't (yet)
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            IQueryable<Mediafile> media = dbContext.Mediafiles
                .Where(m => m.PlanId == PlanId && (artifactTypeId == 0 ? m.ArtifactTypeId == null : m.ArtifactTypeId == artifactTypeId) && m.ReadyToSync && m.PassageId != null)
                .Include(m => m.Passage).ThenInclude(p => p.Section)
                .OrderBy(m => m.PassageId).ThenBy(m => m.VersionNumber);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            return media;
        }
        public override IQueryable<Mediafile> FromCurrentUser(IQueryable<Mediafile>? entities = null)
        {
            return UsersMediafiles(entities ?? GetAll());
        }
        //handles PROJECT_SEARCH_TERM and PROJECT_LIST
        protected override IQueryable<Mediafile> FromProjectList(IQueryable<Mediafile>? entities, string idList)
        {
            return ProjectsMediafiles(entities??GetAll(), idList);
        }
        public Mediafile? Get(int id)
        {
            return GetAll().SingleOrDefault(p => p.Id == id);
        }

    }
}