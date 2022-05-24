using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class PassageRepository : BaseRepository<Passage>
    {

        readonly private SectionRepository SectionRepository;

        public PassageRepository(
        ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            SectionRepository = sectionRepository;
        }
       
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Project> projects)
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, projects);
            return SectionsPassages(entities, sections);
        }

        public IQueryable<Passage> SectionsPassages(IQueryable<Passage> entities, IQueryable<Section> sections)
        {
            return entities.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p);
        }
        public IQueryable<Passage> UsersPassages(IQueryable<Passage> entities, IQueryable<Section>? sections = null)
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
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, plans);
            return SectionsPassages(entities, sections);
        }
        public IQueryable<Passage> ProjectPassages(IQueryable<Passage> entities, string projectid)
        {
            IQueryable<Section> sections = SectionRepository.ProjectSections(dbContext.Sections, projectid);
            return SectionsPassages(entities, sections);
        }
        public IQueryable<Passage> ReadyToSync(int PlanId)
        {
            IQueryable<Section> sections = dbContext.Sections.Where(s => s.PlanId == PlanId);
            IQueryable<Passage> passages = dbContext.Passages.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p).Where(p => p.ReadyToSync).Include(p => p.Section);
            return passages;
        }
        public int? ProjectId(Passage passage)
        {
            return dbContext.Sections.Where(s => s.Id == passage.SectionId).Join(dbContext.Plans, s => s.PlanId, p => p.Id, (s, p) => p).FirstOrDefault()?.ProjectId ;
        }
        public  IQueryable<Passage> Get()
        {
            return base.GetAll();
        }
        public override IQueryable<Passage> FromCurrentUser(IQueryable<Passage>? entities = null)
        {
            return UsersPassages(entities ?? GetAll());
        }
        protected override IQueryable<Passage> FromProjectList(IQueryable<Passage>? entities, string idList)
        {
            return ProjectPassages(entities??GetAll(), idList);
        }
        protected override IQueryable<Passage> FromPlan(QueryLayer layer, string planid)
        {
            if (int.TryParse(planid, out int plan))
                return UsersPassages(base.GetAll(), plan);
            return UsersPassages(base.GetAll(), -1); 
        }
    }
}