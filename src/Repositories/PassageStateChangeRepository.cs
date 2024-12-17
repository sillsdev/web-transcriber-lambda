using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class PassageStateChangeRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        SectionRepository sectionRepository
        ) : BaseRepository<Passagestatechange>(
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
        readonly private SectionRepository SectionRepository = sectionRepository;

        public IQueryable<Passagestatechange> SectionsPassageStateChanges(
            IQueryable<Passagestatechange> entities,
            IQueryable<Section> sections
        )
        {
            return sections
                .Join(dbContext.Passages, s => s.Id, p => p.SectionId, (s, p) => p)
                .Join(entities, p => p.Id, psc => psc.PassageId, (p, psc) => psc);
        }

        public IQueryable<Passagestatechange> UsersPassageStateChanges(
            IQueryable<Passagestatechange> entities,
            IQueryable<Project> projects
        )
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(
                dbContext.Sections,
                projects
            );
            return SectionsPassageStateChanges(entities, sections);
        }

        public IQueryable<Passagestatechange> UsersPassageStateChanges(
            IQueryable<Passagestatechange> entities,
            IQueryable<Section>? sections = null
        )
        {
            if (sections == null)
                sections = SectionRepository.UsersSections(dbContext.Sections);
            return SectionsPassageStateChanges(entities, sections);
        }

        public IQueryable<Passagestatechange> ProjectPassageStateChanges(
            IQueryable<Passagestatechange> entities,
            string projectid
        )
        {
            IQueryable<Section> sections = SectionRepository.ProjectSections(
                dbContext.Sections,
                projectid
            );
            return SectionsPassageStateChanges(entities, sections);
        }

        public override IQueryable<Passagestatechange> FromCurrentUser(
            IQueryable<Passagestatechange>? entities = null
        )
        {
            return UsersPassageStateChanges(entities ?? GetAll());
        }

        public override IQueryable<Passagestatechange> FromProjectList(
            IQueryable<Passagestatechange>? entities,
            string idList
        )
        {
            return ProjectPassageStateChanges(entities ?? GetAll(), idList);
        }
    }
}
