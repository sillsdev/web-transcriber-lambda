using System;
using System.Collections.Generic;
using System.Linq;
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
    public class SectionRepository : BaseRepository<Section>
    {

        private ProjectRepository ProjectRepository;
		private PlanRepository PlanRepository;

        public SectionRepository(
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
		//get my sections in these projects
        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Project> projects)
        {
            var plans = PlanRepository.UsersPlans(PlanRepository.Get(), projects);
            return UsersSections(entities, plans);

        }
        private IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Plan> plans = null)
        {
			//this gets just the plans I have access to
            if (plans == null)
                plans = PlanRepository.Get();

            IEnumerable<int> planIds = plans.Select(p => p.Id);

            //cast this to an ienumerable to avoid an error:A second operation started on this context before a previous operation completed. Any instance members are not guaranteed to be thread safe.
			//something in here is secretly async...but I can't find it
            return ((IEnumerable<Section>)entities).Where(s => planIds.Contains(s.PlanId)).AsQueryable();
        }
        public override IQueryable<Section> Filter(IQueryable<Section> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER)) //unless there is a specific org, we've handled orgs in the Get
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = ProjectRepository.Get().FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSections(entities, projects);
                }
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassageSections()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passagesections");
            //no error...but no passagesections either  return Get().Include(s => s.PassageSections);
            return UsersSections(Include(base.Get(), "passagesections"));

        }

        // This is the set of all Sections that a user has access to.
        public override IQueryable<Section> Get()
        {
            return UsersSections(base.Get());
        }
    }
}