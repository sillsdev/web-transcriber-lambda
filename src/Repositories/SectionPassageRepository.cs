
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Repositories
{
    public class SectionPassageRepository : BaseRepository<SectionPassage>
    {
        protected IJsonApiContext JsonApiContext { get; }

        public SectionPassageRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            JsonApiContext = jsonApiContext;
        }
        public override async Task<SectionPassage> GetAsync(int id)
        {
            SectionPassage entity = await base.GetAsync(id); // dbContext.Sectionpassages.Where(e => e.Id == id).FirstOrDefault();
            if (entity != null && entity.Complete)
                return entity;
            return null;
        }
        public SectionPassage GetByUUID(Guid uuid)
        {
            return dbContext.Sectionpassages.Where(e => e.uuid == uuid).FirstOrDefault();
        }
 

        public List<Section> BulkUpdateSections(List<Section> sections)
        {
            dbContext.UpdateRange(sections);
            dbContext.SaveChanges();
            return sections;
        }

        public List<Passage> BulkUpdatePassages(List<Passage> passages)
        {
            dbContext.UpdateRange(passages);
            dbContext.SaveChanges();
            return passages;
        }
        public Plan UpdatePlanModified(int planId)
        {
            Plan plan = dbContext.Plans.Find(planId);
            dbContext.Plans.Update(plan);
            dbContext.SaveChanges();
            return plan;
        }
        public Passage GetPassage(int id)
        {
            return dbContext.Passages.First(p => p.Id == id);
        }
        public Section GetSection(int id)
        {
            return dbContext.Sections.First(p => p.Id == id);
        }
    }
}
