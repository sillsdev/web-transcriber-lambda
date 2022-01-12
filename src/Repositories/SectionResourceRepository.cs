using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using System.Collections.Generic;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class SectionResourceRepository : BaseRepository<SectionResource>
    {

        private SectionRepository SectionRepository;
        public SectionResourceRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            SectionRepository = sectionRepository;
        }
        #region ScopeToUser
        //get my sections in these projects
        public IQueryable<SectionResource> UsersSectionResources(IQueryable<SectionResource> entities, IQueryable<Project> projects = null)
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
        public override IQueryable<SectionResource> Filter(IQueryable<SectionResource> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    IQueryable<Project> projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                    return UsersSectionResources(entities, projects);
                }
                return UsersSectionResources(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersSectionResources(entities);
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