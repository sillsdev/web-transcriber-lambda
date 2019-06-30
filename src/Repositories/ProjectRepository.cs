﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.Extensions.StringExtensions;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class ProjectRepository : BaseRepository<Project>
    {

        private AppDbContext AppDbContext;

        public ProjectRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            //EntityHooksService<Project> statusUpdateService,
            IDbContextResolver contextResolver
        ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            AppDbContext = contextResolver.GetContext() as AppDbContext;
        }

        public IQueryable<Project> UsersProjects(IQueryable<Project> entities)
        {
            var orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasRole(RoleName.SuperAdmin))
            {
                //if I'm an admin in the org, give me all projects in all groups in that org
                //otherwise give me just the projects in the groups I'm a member of
                var orgadmins = orgIds.Where(o => currentUserRepository.IsOrgAdmin(CurrentUser, o));

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

            var value = filterQuery.Value;
            var op = filterQuery.Operation.ToEnum<FilterOperations>(defaultValue: FilterOperations.eq);

            if (filterQuery.Has(PROJECT_UPDATED_DATE)) {
                var date = value.DateTimeFromISO8601();

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
            return base.Filter(entities, filterQuery);
        }

        public override IQueryable<Project> Sort(IQueryable<Project> entities, List<SortQuery> sortQueries)
        {
            return base.Sort(entities, sortQueries);
        }

    }
}
