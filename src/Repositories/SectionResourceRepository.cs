using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;


namespace SIL.Transcriber.Repositories
{
    public class SectionResourceRepository : BaseRepository<SectionResource>
    {
        private readonly SectionRepository SectionRepository;
        public SectionResourceRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
             SectionRepository sectionRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
                loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            SectionRepository = sectionRepository;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<SectionResource> UsersSectionResources(IQueryable<SectionResource> entities, IQueryable<Project>? projects = null)
        {
            IQueryable<Section> sections = SectionRepository.UsersSections(dbContext.Sections, projects);
            return entities.Join(sections, sr => sr.SectionId, s => s.Id, (sr, s) => sr);
        }

        #endregion
        public IQueryable<SectionResource> ProjectSectionResources(IQueryable<SectionResource> entities, string projectid)
        {

            return UsersSectionResources(entities, dbContext.Projects.Where(p => p.Id.ToString() == projectid));
        }

        #region Overrides
        protected override IQueryable<SectionResource> FromProjectList(IQueryable<SectionResource>? entities, string idList)
        {
            return ProjectSectionResources(entities ?? GetAll(), idList);
        }
        public override IQueryable<SectionResource> FromCurrentUser(IQueryable<SectionResource>? entities = null)
        {
            return UsersSectionResources(entities ?? GetAll());
        }
        #endregion

    }
}