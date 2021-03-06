﻿using System;
using System.Linq;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using SIL.Transcriber.Data;
using System.Collections.Generic;

namespace SIL.Transcriber.Repositories
{
    public class GroupRepository : BaseRepository<Group>
    {
        public GroupRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        public IQueryable<Group> UsersGroups(IQueryable<Group> entities)
        {
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                //if I'm an admin in the org, give me all groups in that org
                //otherwise give me just the groups I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(o => CurrentUser.HasOrgRole(RoleName.Admin, o));

                entities = entities
                       .Where(g => orgadmins.Contains(g.OrganizationId) || CurrentUser.GroupIds.Contains(g.Id));

            }
            return entities;
        }
        public IQueryable<Group> ProjectGroups(IQueryable<Group> entities, string projectid)
        {
            IQueryable<Project> projects = dbContext.Projects.Where(p => p.Id.ToString() == projectid);
            return entities.Join(projects, g => g.Id, p => p.GroupId, (g, p) => g);
        }
        public override IQueryable<Group> Filter(IQueryable<Group> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {
                return entities = entities.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersGroups(entities);
            }
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectGroups(entities, filterQuery.Value);
            }
            return base.Filter(entities, filterQuery);
        }

    }
    /* So far, transcriber doesn't have a use case for this...
    private IQueryable<Group> GetWithOwnerId(IQueryable<Group> query,
                                                    IEnumerable<int> orgIds)
    {
        // Get all groups where the current user
        // is a member of the group owner
        return query
            .Where(g => orgIds.Contains(g.OwnerId));
    }
    */

    /* used with OptionallyFilterOnQueryParam
private IQueryable<Group> GetWithOrganizationContext(IQueryable<Group> query,
                                                     IEnumerable<int>orgIds )
{
    // Get groups owned by the current organization specified
    var allInOrg = query
        .Where(g => (g.OwnerId == OrganizationContext.OrganizationId));
    //if the current user is a member of that organization
    //or if the current user is a superadmin
    var scopeToUser = !CurrentUser.HasRole(RoleName.SuperAdmin) &&
                      !CurrentUser.HasRole(RoleName.OrganizationAdmin, OrganizationContext.OrganizationId);

    if (scopeToUser)
    {
        return allInOrg
            .Where(g => (CurrentUser.GroupIds.Contains(g.Id)));
    }
    return allInOrg;
}

private IQueryable<Group> GetWithUserContext(IQueryable<Group> query,
                                                    IEnumerable<int> orgIds)
{
    //if I'm superadmin - give me all groups
    if (CurrentUser.HasRole(RoleName.SuperAdmin))
        return query;
    //if I'm an admin in the org, give me all groups in that org
    //otherwise give me just the groups I'm a member of
    var orgadmins = orgIds.Where(o => currentUserRepository.IsOrgAdmin(CurrentUser, o));

    return query
           .Where(g => orgadmins.Contains(g.OwnerId) || UserRepository.CurrentUser.GroupIds.Contains(g.Id));

}
*/
}
