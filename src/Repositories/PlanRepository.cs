using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class PlanRepository : BaseRepository<Plan>
    {

        private ProjectRepository ProjectRepository;

        public PlanRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
        }

        public IQueryable<Plan> ProjectsPlans(IQueryable<Plan> entities, IQueryable<Project> projects)
        {
            return entities.Join(projects, (u => u.ProjectId), (p => p.Id), (u, p) => u);
        }
        public IQueryable<Plan> ProjectPlans(IQueryable<Plan> entities, string projectid)
        {
            return ProjectsPlans(entities, ProjectRepository.ProjectProjects(dbContext.Projects, projectid));
        }
        public IQueryable<Plan> UsersPlans(IQueryable<Plan> entities, IQueryable<Project> projects = null)
        {
            if (projects == null)
                projects = ProjectRepository.UsersProjects(dbContext.Projects);

            return ProjectsPlans(entities, projects);
        }
        public override IQueryable<Plan> Filter(IQueryable<Plan> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                IQueryable<Project> projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                 return UsersPlans(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersPlans(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectPlans(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }       
        public Plan GetWithProject(int id)
        {
            return base.Get().Where(p => p.Id == id).Include(p => p.Project).ThenInclude(pr => pr.Organization).FirstOrDefault();
         }
       
    }
}