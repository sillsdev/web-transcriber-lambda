using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;

namespace SIL.Transcriber.Repositories
{
    public class SectionResourceUserRepository : BaseRepository<SectionResourceUser>
    {
        SectionResourceRepository SectionResourceRepository;
        public SectionResourceUserRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
             SectionResourceRepository sectionResourceRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
                loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            SectionResourceRepository = sectionResourceRepository;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<SectionResourceUser> UsersSectionResourceUsers(IQueryable<SectionResourceUser> entities, IQueryable<Project>? projects = null)
        {
            IQueryable<SectionResource> sectionresources = SectionResourceRepository.UsersSectionResources(dbContext.Sectionresources, projects);
            return entities.Join(sectionresources, u => u.SectionResourceId, sr => sr.Id, (u, sr) => u);
        }

        #endregion
        public IQueryable<SectionResourceUser> ProjectSectionResourceUsers(IQueryable<SectionResourceUser> entities, string projectid)
        {

            return UsersSectionResourceUsers(entities, dbContext.Projects.Where(p => p.Id.ToString() == projectid));
        }

        #region Overrides
        protected override IQueryable<SectionResourceUser> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectSectionResourceUsers(base.GetAll(), idList);
        }
        protected override IQueryable<SectionResourceUser> FromCurrentUser(QueryLayer layer)
        {
            return UsersSectionResourceUsers(base.GetAll());
        }
        #endregion
    }
}
