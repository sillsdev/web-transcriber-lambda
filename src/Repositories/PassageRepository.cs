using JsonApiDotNetCore.Configuration;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Utility.Extensions.JSONAPI;

namespace SIL.Transcriber.Repositories
{
    public class PassageRepository : BaseRepository<Passage>
    {
        readonly private SectionRepository SectionRepository;

        public PassageRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository
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
            SectionRepository = sectionRepository;
        }

        public IQueryable<Passage> UsersPassages(
            IQueryable<Passage> entities,
            IQueryable<Project> projects
        )
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(
                dbContext.Sections,
                projects
            );
            return SectionsPassages(entities, sections);
        }

        public IQueryable<Passage> SectionsPassages(
            IQueryable<Passage> entities,
            IQueryable<Section> sections
        )
        {
            return entities.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p);
        }

        public IQueryable<Passage> UsersPassages(
            IQueryable<Passage> entities,
            IQueryable<Section>? sections = null
        )
        {
            if (sections == null)
            {
                sections = SectionRepository.UsersSections(dbContext.Sections);
            }
            return SectionsPassages(entities, sections);
        }

        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, int planid)
        {
            IQueryable<Plan> plans = dbContext.Plans.Where(p => p.Id == planid);
            IQueryable<Section> sections = SectionRepository.UsersSections(
                dbContext.Sections,
                plans
            );
            return SectionsPassages(entities, sections);
        }

        public IQueryable<Passage> ProjectPassages(IQueryable<Passage> entities, string projectid)
        {
            IQueryable<Section> sections = SectionRepository.ProjectSections(
                dbContext.Sections,
                projectid
            );
            return SectionsPassages(entities, sections);
        }

        public IEnumerable<Passage> ReadyToSync(int PlanId)
        {
            IQueryable<Section> sections = dbContext.Sections.Where(s => s.PlanId == PlanId);
            IEnumerable<Passage> passages = dbContext.Passages
                .Join(sections, p => p.SectionId, s => s.Id, (p, s) => p)
                .Include(p => p.Section)
                .ToList()
                .Where(p => p.ReadyToSync);
            return passages;
        }

        public int? ProjectId(Passage passage)
        {
            return dbContext.Sections
                .Where(s => s.Id == passage.SectionId)
                .Join(dbContext.Plans, s => s.PlanId, p => p.Id, (s, p) => p)
                .FirstOrDefault()
                ?.ProjectId;
        }

        public override IQueryable<Passage> FromCurrentUser(IQueryable<Passage>? entities = null)
        {
            return UsersPassages(entities ?? GetAll());
        }

        protected override IQueryable<Passage> FromProjectList(
            IQueryable<Passage>? entities,
            string idList
        )
        {
            return ProjectPassages(entities ?? GetAll(), idList);
        }

        protected override IQueryable<Passage> FromPlan(QueryLayer layer, string planid)
        {
            if (int.TryParse(planid, out int plan))
                return UsersPassages(base.GetAll(), plan);
            return UsersPassages(base.GetAll(), -1);
        }

        protected override IQueryable<Passage> ApplyQueryLayer(QueryLayer layer)
        {
            if (!layer.Filter?.Has(FilterConstants.PLANID) ?? false)
            {
                return base.ApplyQueryLayer(layer);
            }
            if (
                layer.Filter != null
                && int.TryParse(layer.Filter.Value().Replace("'", ""), out int planid)
            )
            {
                layer.Filter = null;
                return FromCurrentUser(
                    SectionsPassages(
                        dbContext.Passages.Include(p => p.Section),
                        dbContext.Sections.Where(s => s.PlanId == planid)
                    )
                );
            }
            return base.ApplyQueryLayer(layer);
        }
    }
}
