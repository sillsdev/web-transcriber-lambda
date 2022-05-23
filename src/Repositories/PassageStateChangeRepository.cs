using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class PassageStateChangeRepository : BaseRepository<PassageStateChange>
    {

        readonly private SectionRepository SectionRepository;

        public PassageStateChangeRepository(
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

        public IQueryable<PassageStateChange> SectionsPassageStateChanges(IQueryable<PassageStateChange> entities, IQueryable<Section> sections)
        {
            return sections.Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p).Join(entities, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
        }
        public IQueryable<PassageStateChange> UsersPassageStateChanges(IQueryable<PassageStateChange> entities, IQueryable<Project> projects)
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, projects);
            return SectionsPassageStateChanges(entities, sections);
        }
        public IQueryable<PassageStateChange> UsersPassageStateChanges(IQueryable<PassageStateChange> entities, IQueryable<Section>? sections = null)
        {
            if (sections == null)
                sections = SectionRepository.UsersSections(dbContext.Sections);
            return SectionsPassageStateChanges(entities, sections);
        }
        public IQueryable<PassageStateChange> ProjectPassageStateChanges(IQueryable<PassageStateChange> entities, string projectid)
        {
            IQueryable<Section> sections = SectionRepository.ProjectSections(dbContext.Sections, projectid);
            return SectionsPassageStateChanges(entities, sections);
        }
        protected override IQueryable<PassageStateChange> GetAll()
        {
            return FromCurrentUser();
        }
        protected override IQueryable<PassageStateChange> FromCurrentUser(QueryLayer? layer = null)
        {
            return UsersPassageStateChanges(base.GetAll());
        }
        protected override IQueryable<PassageStateChange> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectPassageStateChanges(base.GetAll(), idList);
        }
    }
}