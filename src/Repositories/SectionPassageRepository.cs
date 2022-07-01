using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class SectionPassageRepository : BaseRepository<Sectionpassage>
    {
        public SectionPassageRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
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
        { }

        public Sectionpassage? GetByUUID(Guid uuid)
        {
            return dbContext.Sectionpassages.Where(e => e.Uuid == uuid).FirstOrDefault();
        }

        public List<Section> BulkUpdateSections(List<Section> sections)
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
