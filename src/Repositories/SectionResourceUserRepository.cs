using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using JsonApiDotNetCore.Internal.Query;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class SectionResourceUserRepository : BaseRepository<SectionResourceUser>
    {
        SectionResourceRepository SectionResourceRepository;
        public SectionResourceUserRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver, 
            SectionResourceRepository sectionResourceRepository
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            SectionResourceRepository = sectionResourceRepository;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<SectionResourceUser> UsersSectionResourceUsers(IQueryable<SectionResourceUser> entities, IQueryable<Project> projects = null)
        {
            IQueryable<SectionResource> sectionresources = SectionResourceRepository.UsersSectionResources(dbContext.Sectionresources, projects);
            return entities.Join(sectionresources, u => u.SectionResourceId, sr => sr.Id, (u, sr) => u);
        }

        #endregion
        public IQueryable<SectionResourceUser> ProjectSectionResources(IQueryable<SectionResourceUser> entities, string projectid)
        {

            return UsersSectionResourceUsers(entities, dbContext.Projects.Where(p => p.Id.ToString() == projectid));
        }

        #region Overrides
        public override IQueryable<SectionResourceUser> Filter(IQueryable<SectionResourceUser> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    IQueryable<Project> projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSectionResourceUsers(entities, projects);
                }
                return UsersSectionResourceUsers(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersSectionResourceUsers(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectSectionResources(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
