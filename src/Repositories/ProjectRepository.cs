using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using static SIL.Transcriber.Utility.Extensions.StringExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;

namespace SIL.Transcriber.Repositories
{
    public class ProjectRepository : BaseRepository<Project>
    {
        public ProjectRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
        )
            : base(
                targetedFields,
                contextResolver,
                resourceGraph,
                resourceFactory,
                constraintProviders,
                loggerFactory,
                resourceDefinitionAccessor,
                currentUserRepository
            )
        { }

        public IQueryable<Project> ProjectProjects(IQueryable<Project> entities, string projectid)
        {
            //Do not use the stringId here...that evaluates in code instead of in sql and is SLOW
            _ = int.TryParse(projectid, out int id);
            return entities.Where(p => p.Id == id);
        }

        public IQueryable<Project> UsersProjects(IQueryable<Project> entities)
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all projects in all groups in that org
                //otherwise give me just the projects in the groups I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(
                    o => CurrentUserRepository.IsOrgAdmin(CurrentUser, o)
                );

                entities = entities.Where(p =>
                        orgadmins.Contains(p.OrganizationId)
                        || CurrentUser.GroupIds.Contains(p.GroupId)
                );
            }
            return entities;
        }

        //TODO?
        protected IQueryable<Project> FromProjectDate(QueryLayer layer, string projDate)
        { //only project
            DateTime date = projDate.DateTimeFromISO8601();
            /*
                        switch (op)
                        {
                            case FilterOperations.ge:
            */
            return base.GetAll().Where(p => p.DateUpdated > date);
            /*                case FilterOperations.le:
                                return entities
                                    .Where(p => p.DateUpdated < date);
                        }
            */
        }

        public override IQueryable<Project> FromCurrentUser(IQueryable<Project>? entities = null)
        {
            return UsersProjects(entities ?? GetAll());
        }

        public override IQueryable<Project> FromProjectList(
            IQueryable<Project>? entities,
            string idList
        )
        {
            return ProjectProjects(entities ?? GetAll(), idList);
        }

        private static string SettingOrDefault(string? json, string settingName)
        {
            dynamic settings = JObject.Parse(json ?? "");
            return settings [settingName] ?? "";
        }

        public IQueryable<Project> HasIntegrationSetting(
            string integrationName,
            string settingName,
            string value
        )
        {
            IEnumerable<Projectintegration>? pi = dbContext.Integrations
                .Where(i => i.Name == integrationName)
                .Join(
                    dbContext.Projectintegrations.Where(pi => !pi.Archived),
                    i => i.Id,
                    pi => pi.IntegrationId,
                    (p, pi) => pi
                )
                .ToList()
                .Where(pi => SettingOrDefault(pi.Settings, settingName) == value);
            return pi.Join(GetAll(), pi => pi.ProjectId, p => p.Id, (pi, p) => p).AsQueryable();
        }
    }
}
