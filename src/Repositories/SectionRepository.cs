using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class SectionRepository : BaseRepository<Section>
    {

        private PlanRepository PlanRepository;
        public SectionRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            PlanRepository planRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            PlanRepository = planRepository;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Project> projects)
        {
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return PlansSections(entities, plans);
        }

        public IQueryable<Section> PlansSections(IQueryable<Section> entities, IQueryable<Plan> plans)
        {
            return entities.Join(plans, s => s.PlanId, p => p.Id, (s, p) => s);
        }
        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Plan> plans = null)
        {
            //this gets just the plans I have access to
            if (plans == null)
            {
                plans = PlanRepository.UsersPlans(dbContext.Plans);
            }
            return PlansSections(entities, plans);
        }

        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassages()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passages");
            //no error...but no passages either  return Get().Include(s => s.Passages);
            IQueryable<Section> sections = UsersSections(Include(dbContext.Sections, "passages"));
            return sections;
        }
        #endregion
        public IQueryable<Section> ProjectSections(IQueryable<Section> entities, string projectid)
        {
            return PlansSections(entities, PlanRepository.ProjectPlans(dbContext.Plans, projectid));
        }

        #region Overrides
        public override IQueryable<Section> Filter(IQueryable<Section> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER)) 
            {
                if (filterQuery.HasSpecificOrg())
                {
                    IQueryable<Project> projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSections(entities, projects);
                }
                return UsersSections(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersSections(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectSections(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
        #region ParatextSync
        public async Task<IList<SectionSummary>> SectionSummary(int PlanId, string book, int chapter)
        {
            IList<SectionSummary> ss = new List<SectionSummary>();
            var passagewithsection = dbContext.Passages.Join(dbContext.Sections.Where(section => section.PlanId == PlanId), passage => passage.SectionId, section => section.Id, (passage, section) => new { passage, section }).Where(x => x.passage.Book == book && x.passage.StartChapter == chapter);
            await passagewithsection.GroupBy(p => p.section).ForEachAsync(ps =>
              {
                  SectionSummary newss = new SectionSummary()
                  {
                      section = ps.FirstOrDefault().section,
                      Book = book,
                      startChapter = chapter,
                      endChapter = chapter,
                      startVerse = ps.Min(a => a.passage.StartVerse),
                      endVerse = ps.Max(a => a.passage.EndVerse),
                  };
                  ss.Add(newss);
              });
            return ss;
        }
        #endregion

    }
}