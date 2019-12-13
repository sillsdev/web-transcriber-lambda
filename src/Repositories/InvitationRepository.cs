using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;


namespace SIL.Transcriber.Repositories
{ 
    public class InvitationRepository : BaseRepository<Invitation>
    {
        public InvitationRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          //EntityHooksService<Project> statusUpdateService,
          IDbContextResolver contextResolver
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        private IQueryable<Invitation> UsersInvitations(IQueryable<Invitation> entities)
        {
            var orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all invitations in that org
                //otherwise give me invitations just to me
                var orgadmins = orgIds.Where(o => CurrentUser.HasOrgRole(RoleName.Admin, o));

                entities = entities
                       .Where(i => orgadmins.Contains(i.OrganizationId) || CurrentUser.Email == i.Email);

            }
            return entities;
        }
        public override IQueryable<Invitation> Filter(IQueryable<Invitation> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER))
            {

                return entities = entities.FilterByOrganization(filterQuery, allowedOrganizationIds: CurrentUser.OrganizationIds.OrEmpty());
            }
            if (filterQuery.Has(ALLOWED_CURRENTUSER))
            {
                return UsersInvitations(entities);
            }
            return base.Filter(entities, filterQuery);
        }
    }
}
