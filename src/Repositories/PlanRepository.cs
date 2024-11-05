using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class PlanRepository : BaseRepository<Plan>
    {
        readonly private ProjectRepository ProjectRepository;

        public PlanRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository
        )
            : base(
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
            ProjectRepository = projectRepository;
        }

        public IQueryable<Plan> ProjectsPlans(
            IQueryable<Plan> entities,
            IQueryable<Project> projects
        )
        {
            return entities.Join(projects, (u => u.ProjectId), (p => p.Id), (u, p) => u);
        }

        public IQueryable<Plan> ProjectPlans(IQueryable<Plan> entities, string projectid)
        {
            return ProjectsPlans(
                entities,
                ProjectRepository.ProjectProjects(dbContext.Projects, projectid)
            );
        }

        public IQueryable<Plan> ProjectPlans(
            IQueryable<Plan> entities,
            IQueryable<Project> projects
        )
        {
            return ProjectsPlans(entities, projects);
        }

        #region overrides

        public override IQueryable<Plan> FromCurrentUser(IQueryable<Plan>? entities = null)
        {
            return UsersPlans(entities ?? GetAll());
        }

        public override IQueryable<Plan> FromProjectList(IQueryable<Plan>? entities, string idList)
        {
            return ProjectPlans(entities ?? GetAll(), idList);
        }
        #endregion
        public IQueryable<Plan> UsersPlans(
            IQueryable<Plan> entities,
            IQueryable<Project>? projects = null
        )
        {
            projects ??= ProjectRepository.UsersProjects(dbContext.Projects);

            return ProjectsPlans(entities, projects);
        }

        public Plan? GetWithProject(int id)
        {
            return base.GetAll()
                .Where(p => p.Id == id)
                .Include(p => p.Project)
                .ThenInclude(pr => pr.Organization)
                .FirstOrDefault();
        }

        public string DirectoryName(int planId)
        {
            /* this no longer works...project is null */
            Plan? plan = GetWithProject(planId);
            return plan != null ? DirectoryName(plan) : "";
        }
        public string DirectoryName(Plan? plan)
        {
            if (plan == null)
                return "";
            Project? proj = plan.Project;
            proj ??= dbContext.Projects
                    .Where(p => p.Id == plan.ProjectId)
                    .Include(p => p.Organization)
                    .FirstOrDefault();
            Organization? org = proj?.Organization;
            if (org == null && proj?.OrganizationId != null)
                org = dbContext.Organizations
                    .Where(o => o.Id == proj.OrganizationId)
                    .FirstOrDefault();
            return org != null ? org.Slug + "/" + plan.Slug : throw new Exception("No org in DirectoryName");
        }
        public string BibleId(int planid)
        {
            Plan? plan = GetWithProject(planid);
            return plan != null ? BibleId(plan) : "";
        }
        public string BibleId(Plan plan)
        {
            return Bible(plan)?.BibleId ?? "";
        }
        public Bible? Bible(Plan plan)
        {
            if (plan.Project?.OrganizationId != null)
            {
                Organizationbible? orgb = dbContext.OrganizationbiblesData
                .SingleOrDefault(o => o.OrganizationId == plan.Project.OrganizationId);
                if (orgb != null)
                    return orgb.Bible;
            }
            return null;
        }
    }
}
