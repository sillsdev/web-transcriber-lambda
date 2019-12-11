using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
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
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            PlanRepository = planRepository;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Project> projects)
        {
            var plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return UsersSections(entities, plans);
        }

        public IQueryable<Section> UsersSections(IQueryable<Section> entities, IQueryable<Plan> plans = null)
        {
            //this gets just the plans I have access to
            if (plans == null)
            {
                plans = PlanRepository.UsersPlans(dbContext.Plans);
            }

             return entities.Join(plans, s=>s.PlanId, p=>p.Id, (s, p) => s);
        }

        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassageSections()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passagesections");
            //no error...but no passagesections either  return Get().Include(s => s.PassageSections);
            var sections =  UsersSections(Include(dbContext.Sections, "passage-sections"));
            return sections;
        }

        #endregion
        #region Overrides
        public override IQueryable<Section> Filter(IQueryable<Section> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER)) 
            {
                if (filterQuery.HasSpecificOrg())
                {
                    var projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSections(entities, projects);
                }
                return UsersSections(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersSections(entities);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
        #region ParatextSync
        public IQueryable<Section> GetSectionsAtStatus(int projectId, string status)
        {
            var sections = GetWithPassageSections().Include(s => s.Plan);
            var projectsections = dbContext.Projects.Join(sections, p => p.Id, s => s.Plan.ProjectId, (p, s) => s);
            return projectsections.Where(s => s.PassageSections.All(ps => ps.Passage.State == status));
        }
        public async Task<IList<SectionSummary>> SectionSummary(int PlanId, string book, int chapter)
        {
            IList<SectionSummary> ss = new List<SectionSummary>();
            var passagesections = dbContext.Passagesections.Join(dbContext.Sections.Where(section => section.PlanId == PlanId), ps => ps.SectionId, section => section.Id, (ps, section) => new { ps.PassageId, section });
            var passages = dbContext.Passages.Join(passagesections, passage => passage.Id, ps => ps.PassageId, (passage, ps) => new { passage, ps.section }).Where(x => x.passage.Book == book && x.passage.StartChapter == chapter);
            await passages.GroupBy(p => p.section).ForEachAsync(ps =>
              {
                  var newss = new SectionSummary()
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