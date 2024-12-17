using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class SectionResourceRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        SectionRepository sectionRepository
        ) : BaseRepository<Sectionresource>(
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
        private readonly SectionRepository SectionRepository = sectionRepository;

        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Sectionresource> UsersSectionResources(
            IQueryable<Sectionresource> entities,
            IQueryable<Project>? projects = null
        )
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(
                dbContext.Sections,
                projects
            );
            return entities.Join(sections, sr => sr.SectionId, s => s.Id, (sr, s) => sr);
        }

        #endregion
        public IQueryable<Sectionresource> ProjectSectionResources(
            IQueryable<Sectionresource> entities,
            string projectid
        )
        {
            return UsersSectionResources(
                entities,
                dbContext.Projects.Where(p => p.Id.ToString() == projectid)
            );
        }

        #region Overrides
        public override IQueryable<Sectionresource> FromProjectList(
            IQueryable<Sectionresource>? entities,
            string idList
        )
        {
            return ProjectSectionResources(entities ?? GetAll(), idList);
        }

        public override IQueryable<Sectionresource> FromCurrentUser(
            IQueryable<Sectionresource>? entities = null
        )
        {
            return UsersSectionResources(entities ?? GetAll());
        }
        #endregion
    }
}
