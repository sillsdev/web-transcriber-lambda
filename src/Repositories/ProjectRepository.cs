using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.Extensions.StringExtensions;
using SIL.Transcriber.Utility;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class ProjectRepository : BaseRepository<Project>
    {

        public ProjectRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
        ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }

        public IQueryable<Project> ProjectProjects(IQueryable<Project> entities, string projectid)
        {
           return entities.Where(p => p.StringId == projectid);
        }
        public IQueryable<Project> UsersProjects(IQueryable<Project> entities)
        {
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all projects in all groups in that org
                //otherwise give me just the projects in the groups I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(o => currentUserRepository.IsOrgAdmin(CurrentUser, o));

                entities = entities
                       .Where(p => orgadmins.Contains(p.OrganizationId) || CurrentUser.GroupIds.Contains(p.GroupId));
            }
            return entities;
        }
        public override IQueryable<Project> Filter(IQueryable<Project> entities, FilterQuery filterQuery)
        {
            //Get already gives us just these entities = UsersProjects(entities);
            if (filterQuery.Has(ORGANIZATION_HEADER)) 
            {
                return entities.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
            }

            string value = filterQuery.Value;
            FilterOperations op = filterQuery.Operation.ToEnum<FilterOperations>(defaultValue: FilterOperations.eq);

            if (filterQuery.Has(PROJECT_UPDATED_DATE)) {
                DateTime date = value.DateTimeFromISO8601();

                switch(op) {
                    case FilterOperations.ge:
                        return entities
                            .Where(p => p.DateUpdated > date);
                    case FilterOperations.le:
                        return entities
                            .Where(p => p.DateUpdated < date);
                }
            }

            if (filterQuery.Has(PROJECT_SEARCH_TERM)) {
                return entities
                    .Include(p => p.Owner)
                    .Include(p => p.Organization)
                    .Where(p => (
                        EFUtils.Like(p.Name, value) 
                        || EFUtils.Like(p.Language, value)
                        || EFUtils.Like(p.Organization.Name, value)
                        || EFUtils.Like(p.Owner.Name, value)
                    ));
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersProjects(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectProjects(entities, value);
            }
            return base.Filter(entities, filterQuery);
        }

        public override IQueryable<Project> Sort(IQueryable<Project> entities, List<SortQuery> sortQueries)
        {
            return base.Sort(entities, sortQueries);
        }
                private string SettingOrDefault(string json, string settingName)
        {
            dynamic settings = JObject.Parse(json);
            return settings[settingName] ?? "";
        }
        public IQueryable<Project> HasIntegrationSetting(string integrationName, string settingName, string value)
        {
            return dbContext.Integrations.Where(i => i.Name == integrationName).Join(dbContext.Projectintegrations.Where(pi => SettingOrDefault(pi.Settings, settingName) == value && !pi.Archived), i => i.Id, pi => pi.IntegrationId, (p, pi) => pi).Join(Get(), pi => pi.ProjectId, p => p.Id, (pi, p) => p);
        }
    }
}
