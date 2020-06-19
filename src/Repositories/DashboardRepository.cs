using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class DashboardRepository : DefaultEntityRepository<Dashboard, int>
    {
        protected readonly AppDbContext dbContext;

        public DashboardRepository(
              ILoggerFactory loggerFactory,
              IJsonApiContext jsonApiContext,
              IDbContextResolver contextResolver
          ) : base(loggerFactory, jsonApiContext, contextResolver)
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
        private IQueryable<BaseModel> TrainingProjects()
        {
            return dbContext.Projects.Where(p => p.Name.ToLower().Contains("training"));
        }
        private IQueryable<BaseModel>ScripturePlans()
        {
            return dbContext.Plans.Where(p => p.PlantypeId == 1);
        }
        private IQueryable<BaseModel> Transcriptions()
        {
            return dbContext.Mediafiles.Where(m => m.Transcription != null && m.Transcription.Length > 0);
        }
        private IQueryable<BaseModel> Paratext()
        {
            return dbContext.Plans.Where(pl => pl.PlantypeId == 1).Join(dbContext.Sections, pl => pl.Id, s => s.PlanId, (pl, s) => s).Join(dbContext.Passages, s=> s.Id,  p => p.SectionId, (s, p) => p).Where(p => p.State == "done");
        }
        public override IQueryable<Dashboard> Get()
        {
            List<Dashboard> entities = new List<Dashboard>();
            IQueryable<BaseModel> training = TrainingProjects();
            IQueryable<BaseModel> scripture = ScripturePlans();
            IQueryable<BaseModel> transcriptions = Transcriptions();
            IQueryable<BaseModel> paratext = Paratext();
            Dashboard d = new Dashboard
            {
                Id = 1,
                Projects = new DashboardDetail { Total = dbContext.Projects.Count(), Month = getMonthCount(dbContext.Projects), Week = getWeekCount(dbContext.Projects), StringId = "Projects" },
                Training = new DashboardDetail { Total = training.Count(), Month = getMonthCount(training), Week = getWeekCount(training), StringId = "Training" },
                Scripture = new DashboardDetail { Total = scripture.Count(), Month = getMonthCount(scripture), Week = getWeekCount(scripture), StringId = "Scripture Plans" },
                Plans = new DashboardDetail { Total = dbContext.Plans.Count(), Month = getMonthCount(dbContext.Plans), Week = getWeekCount(dbContext.Plans), StringId = "Plans" },
                Passages = new DashboardDetail { Total = dbContext.Passages.Count(), Month = getMonthCount(dbContext.Passages), Week = getWeekCount(dbContext.Passages), StringId = "Passages" },
                Transcriptions = new DashboardDetail { Total = transcriptions.Count(), Month = getMonthCount(transcriptions, true), Week = getWeekCount(transcriptions, true), StringId = "Transcriptions" },
                Paratext = new DashboardDetail { Total = paratext.Count(), Month = getMonthCount(paratext, true), Week = getWeekCount(paratext, true), StringId = "Paratext" },
            };
            entities.Add(d);
            return entities.AsQueryable();
        }
    }
 }