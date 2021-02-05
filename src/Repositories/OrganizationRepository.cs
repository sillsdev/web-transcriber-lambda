using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationRepository : BaseRepository<Organization>
    {
        private ProjectRepository ProjectRepository;
        public OrganizationRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            ProjectRepository = projectRepository;
        }
        public IQueryable<Organization> UsersOrganizations(IQueryable<Organization> entities)
        {
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                return entities.Join(dbContext.Organizationmemberships.Where(om => om.UserId == CurrentUser.Id && !om.Archived), o => o.Id, om => om.OrganizationId, (o, om) => o).GroupBy(o => o.Id).Select(g => g.First());
            }
            return entities;
        }
        public IQueryable<Organization>ProjectOrganizations(IQueryable<Organization> entities, string projectid)
        {
            IQueryable<Project> projects = ProjectRepository.ProjectProjects(dbContext.Projects, projectid);
            return entities.Join(projects, o => o.Id, p => p.OrganizationId, (o, p) => o); //.GroupBy(o => o.Id).Select(g => g.First());
        }
        #region Overrides
        public override IQueryable<Organization> Filter(IQueryable<Organization> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                if (filterQuery.HasSpecificOrg())
                {
                    bool hasSpecifiedOrgId = int.TryParse(filterQuery.Value, out int specifiedOrgId);
                    return UsersOrganizations(entities).Where(om => om.Id == specifiedOrgId) ;
                }
                return UsersOrganizations(entities);
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersOrganizations(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectOrganizations(entities, filterQuery.Value);
            }
            if (filterQuery.Has(DATA_START_INDEX)) //ignore
            {
                return entities;
            }
            if (filterQuery.Has(PROJECT_SEARCH_TERM)) //ignore
            {
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}
