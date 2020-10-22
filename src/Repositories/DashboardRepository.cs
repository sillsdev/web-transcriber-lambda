using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
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

        public DashboardRepository(
              ILoggerFactory loggerFactory,
              IJsonApiContext jsonApiContext,
              CurrentUserRepository currentUserRepository,
              AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
        }
        private int getMonthCount(IQueryable<BaseModel> entities, bool updated = false)
        {
            DateTime checkDate = DateTime.Now.AddDays(-30);
            return entities.Where(e => (updated ? e.DateUpdated : e.DateCreated) > checkDate).Count();
        }
        private int getWeekCount(IQueryable<BaseModel> entities, bool updated = false)
        {
            DateTime checkDate = DateTime.Now.AddDays(-7);
            return entities.Where(e => (updated ? e.DateUpdated : e.DateCreated) > checkDate).Count();
        }
        private IQueryable<BaseModel> Projects()
        {
            return dbContext.Projects.Where(p => !p.Archived);
        }
        private IQueryable<BaseModel> TrainingProjects()
        {
            return dbContext.Projects.Where(p => !p.Archived && p.Name.ToLower().Contains("training"));
        }
        private IQueryable<BaseModel> Plans()
        {
            return dbContext.Plans.Where(p => !p.Archived);
        }
        private IQueryable<BaseModel> Passages()
        {
            return dbContext.Passages.Where(p => !p.Archived);
        }
        private IQueryable<BaseModel> ScripturePlans()
        {
            return dbContext.Plans.Where(p => !p.Archived && p.PlantypeId == 1);
        }
        private IQueryable<BaseModel> Transcriptions()
        {
            return dbContext.Mediafiles.Where(m => !m.Archived && m.Transcription != null && m.Transcription.Length > 0);
        }
        private IQueryable<BaseModel> Paratext()
        {
            return dbContext.Plans.Where(pl => !pl.Archived && pl.PlantypeId == 1).Join(dbContext.Sections, pl => pl.Id, s => s.PlanId, (pl, s) => s).Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Where(p => p.State == "done");
        }
        public override IQueryable<Dashboard> Get()
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
                Projects = new DashboardDetail { Total = projects.Count(), Month = getMonthCount(projects), Week = getWeekCount(projects), StringId = "Projects" },
                Training = new DashboardDetail { Total = training.Count(), Month = getMonthCount(training), Week = getWeekCount(training), StringId = "Training" },
                Scripture = new DashboardDetail { Total = scripture.Count(), Month = getMonthCount(scripture), Week = getWeekCount(scripture), StringId = "Scripture Plans" },
                Plans = new DashboardDetail { Total = plans.Count(), Month = getMonthCount(plans), Week = getWeekCount(plans), StringId = "Plans" },
                Passages = new DashboardDetail { Total = passages.Count(), Month = getMonthCount(passages), Week = getWeekCount(passages), StringId = "Passages" },
                Transcriptions = new DashboardDetail { Total = transcriptions.Count(), Month = getMonthCount(transcriptions, true), Week = getWeekCount(transcriptions, true), StringId = "Transcriptions" },
                Paratext = new DashboardDetail { Total = paratext.Count(), Month = getMonthCount(paratext, true), Week = getWeekCount(paratext, true), StringId = "Paratext" },
            };
            entities.Add(d);
            return entities.AsQueryable();
        }
    }
}