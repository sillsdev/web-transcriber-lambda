using JsonApiDotNetCore.Configuration;
using Microsoft.EntityFrameworkCore;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class SectionPassageRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Sectionpassage>(
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
        public Sectionpassage? GetByUUID(Guid uuid)
        {
            return dbContext.Sectionpassages.Where(e => e.Uuid == uuid).FirstOrDefault();
        }

        public async Task<List<Section>> BulkUpdateSections(List<Section> sections)
        {
            dbContext.UpdateRange(sections);
            _ = dbContext.SaveChanges();
            return sections;
        }

        public List<Section> BulkDeleteSections(List<Section> sections)
        {
            dbContext.RemoveRange(sections);
            _ = dbContext.SaveChanges();
            return sections;
        }

        public async Task<int> BulkDeleteSectionsByIds(List<int> sectionIds)
        {
            if (sectionIds == null || sectionIds.Count == 0)
                return 0;

            // Perform a set-based delete without loading entities into memory
            int deleted = await dbContext.Sections
                .Where(s => sectionIds.Contains(s.Id))
                .ExecuteDeleteAsync();
            return deleted;
        }

        public List<Passage> BulkUpdatePassages(List<Passage> passages)
        {
            dbContext.UpdateRange(passages);
            _ = dbContext.SaveChanges();
            return passages;
        }

        public List<Passage> BulkDeletePassages(List<Passage> passages)
        {
            dbContext.RemoveRange(passages);
            _ = dbContext.SaveChanges();
            return passages;
        }

        public async Task<int> BulkDeletePassagesByIds(List<int> passageIds)
        {
            if (passageIds == null || passageIds.Count == 0)
                return 0;

            int deleted = await dbContext.Passages
                .Where(p => passageIds.Contains(p.Id))
                .ExecuteDeleteAsync();
            return deleted;
        }

        public Section? UpdateSectionModified(int sectionId)
        {
            Section? section = dbContext.Sections.Find(sectionId);
            if (section != null)
            {
                _ = dbContext.Sections.Update(section);
                _ = dbContext.SaveChanges();
            }
            return section;
        }

        public Plan? UpdatePlanModified(int planId)
        {
            Plan? plan = dbContext.Plans.Find(planId);
            if (plan != null)
            {
                plan.SectionCount = dbContext.Sections
                    .Where(s => s.PlanId == planId && !s.Archived)
                    .Count();
                _ = dbContext.Plans.Update(plan);
                _ = dbContext.SaveChanges();
            }
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

        public override IQueryable<Sectionpassage> FromCurrentUser(
            IQueryable<Sectionpassage>? entities = null
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Sectionpassage> FromProjectList(
            IQueryable<Sectionpassage>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
