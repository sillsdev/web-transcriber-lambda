using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class MediafileRepository : BaseRepository<Mediafile>
    {

        private ProjectRepository ProjectRepository;
        private PlanRepository PlanRepository;
        private AppDbContext AppDbContext;

        public MediafileRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository,
            PlanRepository planRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
            AppDbContext = contextResolver.GetContext() as AppDbContext;
            PlanRepository = planRepository;
        }

        //get my Mediafiles in these projects
        public IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, IQueryable<Project> projects)
        {
            //this gets just the passages I have access to in these projects
            var plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersMediafiles(entities, plans);
        }

        private IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, IQueryable<Plan> plans = null)
        {
            if (plans == null)
                plans = PlanRepository.UsersPlans(dbContext.Plans);

            return entities.Join(plans, m => m.PlanId, p => p.Id, (m, p) => m);
        }
        public override IQueryable<Mediafile> Filter(IQueryable<Mediafile> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
				return UsersMediafiles(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersMediafiles(entities);
            }
            return base.Filter(entities, filterQuery);
        }
        public Mediafile Get(int id)
        {
            return Get().SingleOrDefault(p => p.Id == id);
        }
    }
}