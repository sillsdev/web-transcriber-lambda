using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace SIL.Transcriber.Repositories
{
    public class DashboardRepository : AppDbContextRepository<Dashboard>
    {
        protected readonly AppDbContext dbContext;

        public DashboardRepository(ITargetedFields targetedFields,
            AppDbContextResolver contextResolver, IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders, ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
        }
        private static int GetMonthCount(IQueryable<BaseModel> entities, bool updated = false)
        {
            DateTime checkDate = DateTime.Now.AddDays(-30).ToUniversalTime();
            return entities.Where(e => (updated ? e.DateUpdated : e.DateCreated) > checkDate).Count();
        }
        private static int GetWeekCount(IQueryable<BaseModel> entities, bool updated = false)
        {
            DateTime checkDate = DateTime.Now.AddDays(-7).ToUniversalTime();
            return entities.Where(e => (updated ? e.DateUpdated : e.DateCreated) > checkDate).Count();
        }


        private IQueryable<Project> Projects()
        {
            return dbContext.Projects.Where(p => !p.Archived);
        }
        private IQueryable<Plan> TrainingProjects()
        {
#pragma warning disable CS8604 // Possible null reference argument.
            return Plans().Where(p => p.Tags != null && JObject.Parse(p.Tags)["training"] != null && JObject.Parse(p.Tags)["training"].Value<bool>()||false);
        }
        private IQueryable<Plan> Plans()
        {
            return dbContext.Plans.Where(p => !p.Archived && (p.Tags == null || JObject.Parse(p.Tags)["testing"] == null || !JObject.Parse(p.Tags)["testing"].Value<bool>()));
#pragma warning restore CS8604 // Possible null reference argument.
        }
        private IQueryable<Passage> Passages()
        {
            return Plans().Join(dbContext.Sections, pl => pl.Id, s => s.PlanId, (pl, s) => s).Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Where(p => !p.Archived);
        }
        private IQueryable<Plan> ScripturePlans()
        {
            return Plans().Where(p => p.PlantypeId == 1);
        }
        private IQueryable<Mediafile> Transcriptions()
        {
            return Plans().Join(dbContext.Mediafiles.Where(m => !m.Archived && m.Transcription != null && m.Transcription.Length > 0), pl => pl.Id, m => m.PlanId, (pl, m) => m);
        }
        private IQueryable<Mediafile> Paratext()
        {
            return ScripturePlans().Join(dbContext.Mediafiles, pl => pl.Id, m => m.PlanId, (pl, m) => m).Where(m => m.Transcriptionstate == "done");
        }
        public new IQueryable<Dashboard> GetAll()
        {
            List<Dashboard> entities = new List<Dashboard>();
            IQueryable<BaseModel> projects = Projects();
            IQueryable<BaseModel> training = TrainingProjects();
            IQueryable<BaseModel> plans = Plans();
            IQueryable<BaseModel> scripture = ScripturePlans();
            IQueryable<BaseModel> passages = Passages();
            IQueryable<BaseModel> transcriptions = Transcriptions();
            IQueryable<BaseModel> paratext = Paratext();
            Dashboard d = new Dashboard
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