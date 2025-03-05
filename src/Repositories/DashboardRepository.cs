using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;


namespace SIL.Transcriber.Repositories
{
    public class DashboardRepository(ITargetedFields targetedFields,
        AppDbContextResolver contextResolver, IResourceGraph resourceGraph, IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders, ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor
            ) : AppDbContextRepository<Dashboard>(targetedFields, contextResolver, resourceGraph, resourceFactory,
            constraintProviders, loggerFactory, resourceDefinitionAccessor)
    {
        protected readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();

        private static int GetMonthCount(IEnumerable<BaseModel> entities, bool updated = false)
        {
            DateTime checkDate = DateTime.Now.AddDays(-30).ToUniversalTime();
            return entities.Where(e => (updated ? e.DateUpdated : e.DateCreated) > checkDate).Count();
        }
        private static int GetWeekCount(IEnumerable<BaseModel> entities, bool updated = false)
        {
            DateTime checkDate = DateTime.Now.AddDays(-7).ToUniversalTime();
            return entities.Where(e => (updated ? e.DateUpdated : e.DateCreated) > checkDate).Count();
        }


        private IQueryable<Project> Projects()
        {
            return dbContext.Projects.Where(p => !p.Archived);
        }
        private IEnumerable<Plan> TrainingPlans()
        {
            List<Plan> plans = [.. dbContext.Plans.Where(p => p.Tags != null)];
            return plans.Where(p=> JObject.Parse(p.Tags ?? "{}") ["training"]?.Value<bool?>() ?? false);
        }
        private IEnumerable<Plan> NonTestingPlans()
        {
            List<Plan> plans =  [.. dbContext.Plans.Where(p => !p.Archived)];
            return plans.Where(p => !(JObject.Parse(p.Tags??"{}")["testing"]?.Value<bool?>() ?? false));
        }
        private IEnumerable<Passage> Passages()
        {
            return NonTestingPlans().Join(dbContext.Sections, pl => pl.Id, s => s.PlanId, (pl, s) => s).Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Where(p => !p.Archived);
        }
        private IEnumerable<Plan> ScripturePlans()
        {
            return NonTestingPlans().Where(p => p.PlantypeId == 1);
        }
        private IEnumerable<Mediafile> Transcriptions()
        {
            return NonTestingPlans().Join(dbContext.Mediafiles.Where(m => !m.Archived && m.Transcription != null && m.Transcription.Length > 0), pl => pl.Id, m => m.PlanId, (pl, m) => m);
        }
        private IEnumerable<Mediafile> Paratext()
        {
            return ScripturePlans().Join(dbContext.Mediafiles, pl => pl.Id, m => m.PlanId, (pl, m) => m).Where(m => m.Transcriptionstate == "done");
        }
        public new IQueryable<Dashboard> GetAll()
        {
            List<Dashboard> entities = [];
            IEnumerable<BaseModel> projects = Projects().ToList();
            IEnumerable<BaseModel> training = TrainingPlans();
            IEnumerable<BaseModel> plans = NonTestingPlans().ToList();
            IEnumerable<BaseModel> scripture = ScripturePlans().ToList();
            IEnumerable<BaseModel> passages = Passages().ToList();
            IEnumerable<BaseModel> transcriptions = Transcriptions().ToList();
            IEnumerable<BaseModel> paratext = Paratext().ToList();
            Dashboard d = new()
            {
                Id = 1,
                Projects = new DashboardDetail { Total = projects.Count(), Month = GetMonthCount(projects), Week = GetWeekCount(projects), StringId = "Projects" },
                Training = new DashboardDetail { Total = training.Count(), Month = GetMonthCount(training), Week = GetWeekCount(training), StringId = "Training" },
                Scripture = new DashboardDetail { Total = scripture.Count(), Month = GetMonthCount(scripture), Week = GetWeekCount(scripture), StringId = "Scripture Plans" },
                Plans = new DashboardDetail { Total = plans.Count(), Month = GetMonthCount(plans), Week = GetWeekCount(plans), StringId = "Plans" },
                Passages = new DashboardDetail { Total = passages.Count(), Month = GetMonthCount(passages), Week = GetWeekCount(passages), StringId = "Passages" },
                Transcriptions = new DashboardDetail { Total = transcriptions.Count(), Month = GetMonthCount(transcriptions, true), Week = GetWeekCount(transcriptions, true), StringId = "Transcriptions" },
                Paratext = new DashboardDetail { Total = paratext.Count(), Month = GetMonthCount(paratext, true), Week = GetWeekCount(paratext, true), StringId = "Paratext" },
            };
            entities.Add(d);
            return entities.AsQueryable();
        }
    }
}