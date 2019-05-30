using System;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility.Extensions.JSONAPI;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class GroupRepository : BaseRepository<Group>
    {
        public GroupRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            IDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        private IQueryable<Group> UsersGroups(IQueryable<Group> entities)
        {
            var orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasRole(RoleName.SuperAdmin))
            {
                //if I'm an admin in the org, give me all groups in that org
                //otherwise give me just the groups I'm a member of
                var orgadmins = orgIds.Where(o => CurrentUser.HasRole(RoleName.OrganizationAdmin, o));

                entities = entities
                       .Where(g => orgadmins.Contains(g.OrganizationId) || CurrentUser.GroupIds.Contains(g.Id));

            }
            return entities;
        }
        public override IQueryable<Group> Filter(IQueryable<Group> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {

                return entities = entities.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
            }
            return base.Filter(entities, filterQuery);
            /* deprecated....
             return entities.OptionallyFilterOnQueryParam(filterQuery,
                                                       "organization-header",
                                                       UserRepository,
                                                       CurrentUserContext,
                                                       GetWithFilter,
                                                       base.Filter,
                                                       GetWithUserContext,
                                                       GetWithOrganizationContext); 
                                                       */
        }
        // This is the set of all groups that a user has access to.
        // If a group would ever need to be accessed outside of this set of group,
        // this method should not be used.
        public override IQueryable<Group> Get()
        {
            return UsersGroups(base.Get());
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
