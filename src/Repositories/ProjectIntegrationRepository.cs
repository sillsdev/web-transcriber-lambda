using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class ProjectIntegrationRepository : BaseRepository<ProjectIntegration>
    {

        private ProjectRepository ProjectRepository;

        public ProjectIntegrationRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository, 
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
        }
        public IQueryable<ProjectIntegration> UsersProjectIntegrations(IQueryable<ProjectIntegration> entities, IQueryable<Project> projects = null)
        {
           if (projects == null)
                projects = ProjectRepository.UsersProjects(dbContext.Projects);

            return entities.Join(projects, (u => u.ProjectId), (p => p.Id), (u, p) => u);
            
        }

        public override IQueryable<ProjectIntegration> Filter(IQueryable<ProjectIntegration> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                return UsersProjectIntegrations(entities); //, projects);
            }

            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersProjectIntegrations(entities);
            }
            return base.Filter(entities, filterQuery);
        }
        public string IntegrationSettings(int projectId, string integration)
        {
            ProjectIntegration projectIntegration = Get().Where(pi => pi.ProjectId == projectId).Join(dbContext.Integrations.Where(i => i.Name == integration), pi => pi.IntegrationId, i => i.Id, (pi, i) => pi).FirstOrDefault();
            if (projectIntegration == null)
                return "";
            return projectIntegration.Settings;
        }

    }
}