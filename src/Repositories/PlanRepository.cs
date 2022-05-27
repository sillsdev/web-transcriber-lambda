using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class PlanRepository : BaseRepository<Plan>
    {

        readonly private ProjectRepository ProjectRepository;

        public PlanRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
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
        public IQueryable<Plan> ProjectPlans(IQueryable<Plan> entities, IQueryable<Project> projects)
        {
            return ProjectsPlans(entities, projects);
        }

        #region overrides

        public override IQueryable<Plan> FromCurrentUser(IQueryable<Plan>? entities = null)
        {
            return UsersPlans(entities ?? GetAll());
        }
        protected override IQueryable<Plan> FromProjectList(IQueryable<Plan>? entities, string idList)
        {
            return ProjectPlans(entities ?? GetAll(), idList);
        }
        #endregion
        public IQueryable<Plan> UsersPlans(IQueryable<Plan> entities, IQueryable<Project>? projects = null)
        {
            if (projects == null)
                projects = ProjectRepository.UsersProjects(dbContext.Projects);

            return ProjectsPlans(entities, projects);
        }
        public Plan? GetWithProject(int id)
        {
            return base.GetAll().Where(p => p.Id == id).Include(p => p.Project).ThenInclude(pr => pr.Organization).FirstOrDefault();
        }
    }
}