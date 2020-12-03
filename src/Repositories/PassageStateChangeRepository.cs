using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class PassageStateChangeRepository : BaseRepository<PassageStateChange>
    {

        private SectionRepository SectionRepository;

        public PassageStateChangeRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            SectionRepository sectionRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
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
        public IQueryable<PassageStateChange> UsersPassageStateChanges(IQueryable<PassageStateChange> entities, IQueryable<Section> sections = null)
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

        public override IQueryable<PassageStateChange> Filter(IQueryable<PassageStateChange> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                IQueryable<Project> projects = dbContext.Projects.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
                return UsersPassageStateChanges(entities, projects);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersPassageStateChanges(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectPassageStateChanges(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }

    }
}