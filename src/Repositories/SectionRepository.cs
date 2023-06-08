using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class SectionRepository : BaseRepository<Section>
    {
        readonly private PlanRepository PlanRepository;

        public SectionRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            PlanRepository planRepository
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
            PlanRepository = planRepository;
        }

        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Section> UsersSections(
            IQueryable<Section> entities,
            IQueryable<Project>? projects
        )
        {
            IQueryable<Plan> plans = PlanRepository.UsersPlans(dbContext.Plans, projects);
            return PlansSections(entities, plans);
        }

        public IQueryable<Section> PlansSections(
            IQueryable<Section> entities,
            IQueryable<Plan> plans
        )
        {
            return entities.Where(e => !e.Archived).Join(plans, s => s.PlanId, p => p.Id, (s, p) => s);
        }

        public IQueryable<Section> UsersSections(
            IQueryable<Section> entities,
            IQueryable<Plan>? plans = null
        )
        {
            //this gets just the plans I have access to
            plans ??= PlanRepository.UsersPlans(dbContext.Plans);
            return PlansSections(entities, plans);
        }

        // This is the set of all Sections that a user has access to.
        public IQueryable<Section> GetWithPassages()
        {
            //you'd think this would work...but you'd be wrong;
            //return Include(Get(), "passages");
            //no error...but no passages either  return Get().Include(s => s.Passages);
            IQueryable<Section> sections = UsersSections(GetAll().Include(Tables.Passages));
            return sections;
        }
        #endregion
        public IQueryable<Section> ProjectSections(IQueryable<Section> entities, string projectid)
        {
            return PlansSections(entities, PlanRepository.ProjectPlans(dbContext.Plans, projectid));
        }

        #region Overrides
        public override IQueryable<Section> FromCurrentUser(IQueryable<Section>? entities = null)
        {
            return UsersSections(entities ?? GetAll());
        }

        public override IQueryable<Section> FromProjectList(
            IQueryable<Section>? entities,
            string idList
        )
        {
            return ProjectSections(entities ?? GetAll(), idList);
        }
        #endregion
        #region ParatextSync
        public async Task<IList<SectionSummary>> SectionSummary(
            int PlanId,
            string book,
            int chapter
        )
        {
            IList<SectionSummary> ss = new List<SectionSummary>();
            var passagewithsection = dbContext.Passages
                .Join(
                    dbContext.Sections.Where(section => section.PlanId == PlanId),
                    passage => passage.SectionId,
                    section => section.Id,
                    (passage, section) => new { passage, section }
                )
                .Where(x => x.passage.Book == book && x.passage.StartChapter == chapter);
            await passagewithsection
                .GroupBy(p => p.section)
                .ForEachAsync(ps => {
                    SectionSummary newss =
                        new()
                        {
                            section = ps.FirstOrDefault()?.section ?? new(),
                            Book = book,
                            StartChapter = chapter,
                            EndChapter = chapter,
                            StartVerse = ps.Min(a => a.passage.StartVerse),
                            EndVerse = ps.Max(a => a.passage.EndVerse),
                        };
                    ss.Add(newss);
                });
            return ss;
        }
        #endregion
    }
}
