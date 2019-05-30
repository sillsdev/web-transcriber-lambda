using System;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
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
            PlanRepository = planRepository;
        }

        //get my Mediafiles in these projects
        public IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, IQueryable<Project> projects)
        {
            //this gets just the passages I have access to in these projects
            var plans = PlanRepository.UsersPlans(entities.Select(mf => mf.Plan), projects);
            return UsersMediafiles(entities, plans);
        }

        private IQueryable<Mediafile> UsersMediafiles(IQueryable<Mediafile> entities, IQueryable<Plan> plans = null)
        {
            if (plans == null)
                plans = PlanRepository.Get();

            return entities.Where(m => plans.Contains(m.Plan));
        }
        public override IQueryable<Mediafile> Filter(IQueryable<Mediafile> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
					return UsersMediafiles(entities, projects);
                }
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
        public IQueryable<Mediafile> GetInternal()
        {
            return base.Get();
        }
        public Mediafile GetInternal(int id)
        {
            return base.Get().SingleOrDefault(p => p.Id == id);
        }

        // This is the set of all Mediafiles that a user has access to.
        public override IQueryable<Mediafile> Get()
        {
            return UsersMediafiles(base.Get());
        }
    }
}