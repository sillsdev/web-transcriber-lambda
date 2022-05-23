using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;


namespace SIL.Transcriber.Repositories
{
    public class ProjectIntegrationRepository : BaseRepository<ProjectIntegration>
    {

        readonly private ProjectRepository ProjectRepository;

        public ProjectIntegrationRepository(

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
        public IQueryable<ProjectIntegration> ProjectProjectIntegrations(IQueryable<ProjectIntegration> entities, IQueryable<Project> projects)
        {
            return entities.Join(projects, (u => u.ProjectId), (p => p.Id), (u, p) => u);          
        }
        public IQueryable<ProjectIntegration> UsersProjectIntegrations(IQueryable<ProjectIntegration> entities, IQueryable<Project>? projects = null)
        {
            if (projects == null)
                projects = ProjectRepository.UsersProjects(dbContext.Projects);
            return ProjectProjectIntegrations(entities, projects);
        }
        public IQueryable<ProjectIntegration> ProjectProjectIntegrations(IQueryable<ProjectIntegration> entities, string projectid)
        {
            return ProjectProjectIntegrations(entities, ProjectRepository.ProjectProjects(dbContext.Projects, projectid));
        }
        protected override IQueryable<ProjectIntegration> GetAll()
        {
            return FromCurrentUser();
        }
        protected override IQueryable<ProjectIntegration> FromCurrentUser(QueryLayer? layer = null)
        {
            return UsersProjectIntegrations(base.GetAll());
        }
        protected override IQueryable<ProjectIntegration> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectProjectIntegrations(base.GetAll(), idList);
        }
        public string IntegrationSettings(int projectId, string integration)
        {
            ProjectIntegration? projectIntegration = GetAll().Where(pi => pi.ProjectId == projectId).Join(dbContext.Integrations.Where(i => i.Name == integration), pi => pi.IntegrationId, i => i.Id, (pi, i) => pi).FirstOrDefault();
            return projectIntegration?.Settings??"";
        }

    }
}
