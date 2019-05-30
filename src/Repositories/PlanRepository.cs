using System;
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
                projects = ProjectRepository.Get();

            return entities.Where(p => projects.Contains(p.Project));

        }
        public override IQueryable<Plan> Filter(IQueryable<Plan> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersPlans(entities, projects);
                }
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }        // This is the set of all plans that a user has access to.

        // This is the set of all plans that a user has access to.
        public override IQueryable<Plan> Get()
        {
            return UsersPlans(base.Get());
        }
        
        public Plan GetWithProject(int id)
        {
            return base.Get().Where(p => p.Id == id).Include(p => p.Project).ThenInclude(pr => pr.Organization).FirstOrDefault();
         }
       
    }
}