using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class SectionResourceUserRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        SectionResourceRepository sectionResourceRepository
        ) : BaseRepository<Sectionresourceuser>(
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
        private readonly SectionResourceRepository SectionResourceRepository = sectionResourceRepository;

        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<Sectionresourceuser> UsersSectionResourceUsers(
            IQueryable<Sectionresourceuser> entities,
            IQueryable<Project>? projects = null
        )
        {
            IQueryable<Sectionresource> sectionresources =
                SectionResourceRepository.UsersSectionResources(
                    dbContext.Sectionresources,
                    projects
                );
            return entities.Join(
                sectionresources,
                u => u.SectionResourceId,
                sr => sr.Id,
                (u, sr) => u
            );
        }

        #endregion
        public IQueryable<Sectionresourceuser> ProjectSectionResourceUsers(
            IQueryable<Sectionresourceuser> entities,
            string projectid
        )
        {
            return UsersSectionResourceUsers(
                entities,
                dbContext.Projects.Where(p => p.Id.ToString() == projectid)
            );
        }

        #region Overrides
        public override IQueryable<Sectionresourceuser> FromProjectList(
            IQueryable<Sectionresourceuser>? entities,
            string idList
        )
        {
            return ProjectSectionResourceUsers(entities ?? GetAll(), idList);
        }

        public override IQueryable<Sectionresourceuser> FromCurrentUser(
            IQueryable<Sectionresourceuser>? entities = null
        )
        {
            return UsersSectionResourceUsers(entities ?? GetAll());
        }
        #endregion
    }
}
