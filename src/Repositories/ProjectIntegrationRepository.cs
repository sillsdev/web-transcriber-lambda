using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class ProjectIntegrationRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        ProjectRepository projectRepository
        ) : BaseRepository<Projectintegration>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        readonly private ProjectRepository ProjectRepository = projectRepository;

        public IQueryable<Projectintegration> ProjectProjectIntegrations(
            IQueryable<Projectintegration> entities,
            IQueryable<Project> projects
        )
        {
            return entities.Join(projects, (u => u.ProjectId), (p => p.Id), (u, p) => u);
        }

        public IQueryable<Projectintegration> UsersProjectIntegrations(
            IQueryable<Projectintegration> entities,
            IQueryable<Project>? projects = null
        )
        {
            if (projects == null)
                projects = ProjectRepository.UsersProjects(dbContext.Projects);
            return ProjectProjectIntegrations(entities, projects);
        }

        public IQueryable<Projectintegration> ProjectProjectIntegrations(
            IQueryable<Projectintegration> entities,
            string projectid
        )
        {
            return ProjectProjectIntegrations(
                entities,
                ProjectRepository.ProjectProjects(dbContext.Projects, projectid)
            );
        }

        public override IQueryable<Projectintegration> FromCurrentUser(
            IQueryable<Projectintegration>? entities = null
        )
        {
            return UsersProjectIntegrations(entities ?? GetAll());
        }

        public override IQueryable<Projectintegration> FromProjectList(
            IQueryable<Projectintegration>? entities,
            string idList
        )
        {
            return ProjectProjectIntegrations(entities ?? GetAll(), idList);
        }

        public string IntegrationSettings(int projectId, string integration)
        {
            Projectintegration? projectIntegration = GetAll()
                .Where(pi => pi.ProjectId == projectId)
                .Join(
                    dbContext.Integrations.Where(i => i.Name == integration),
                    pi => pi.IntegrationId,
                    i => i.Id,
                    (pi, i) => pi
                )
                .FirstOrDefault();
            return projectIntegration?.Settings ?? "";
        }
    }
}
