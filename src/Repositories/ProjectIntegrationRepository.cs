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

        public override IQueryable<ProjectIntegration> FromCurrentUser(IQueryable<ProjectIntegration>? entities = null)
        {
            return UsersProjectIntegrations(entities ?? GetAll());
        }
        protected override IQueryable<ProjectIntegration> FromProjectList(IQueryable<ProjectIntegration>? entities, string idList)
        {
            return ProjectProjectIntegrations(entities ?? GetAll(), idList);
        }
        public string IntegrationSettings(int projectId, string integration)
        {
            ProjectIntegration? projectIntegration = GetAll().Where(pi => pi.ProjectId == projectId).Join(dbContext.Integrations.Where(i => i.Name == integration), pi => pi.IntegrationId, i => i.Id, (pi, i) => pi).FirstOrDefault();
            return projectIntegration?.Settings??"";
        }

    }
}
