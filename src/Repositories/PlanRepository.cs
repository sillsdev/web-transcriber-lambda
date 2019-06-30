﻿using System;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class PlanRepository : BaseRepository<Plan>
    {

        private ProjectRepository ProjectRepository;
        private OrganizationRepository OrganizationRepository;

        public PlanRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository,
            OrganizationRepository organizationRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<Plan> UsersPlans(IQueryable<Plan> entities, IQueryable<Project> projects = null)
        {
            if (projects == null)
                projects = ProjectRepository.UsersProjects(dbContext.Projects);

            return entities.Where(p => projects.Contains(p.Project));

        }
        public override IQueryable<Plan> Filter(IQueryable<Plan> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                 var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                 return UsersPlans(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersPlans(entities);
            }
            return base.Filter(entities, filterQuery);
        }       
        public Plan GetWithProject(int id)
        {
            return base.Get().Where(p => p.Id == id).Include(p => p.Project).ThenInclude(pr => pr.Organization).FirstOrDefault();
         }
       
    }
}