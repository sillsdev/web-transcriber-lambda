using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Data;
using System.Collections.Generic;

namespace SIL.Transcriber.Repositories
{ 
    public class InvitationRepository : BaseRepository<Invitation>
    {
        private GroupRepository GroupRepository;
        public InvitationRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          GroupRepository groupRepository,
          AppDbContextResolver contextResolver
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
            GroupRepository = groupRepository;
        }
        private IQueryable<Invitation> UsersInvitations(IQueryable<Invitation> entities)
        {
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all invitations in that org
                //otherwise give me invitations just to me
                IEnumerable<int> orgadmins = orgIds.Where(o => CurrentUser.HasOrgRole(RoleName.Admin, o));
                string currentEmail = CurrentUser.Email.ToLower();

                entities = entities
                       .Where(i => orgadmins.Contains(i.OrganizationId) || currentEmail == i.Email.ToLower());
            }
            return entities;
        }
        private IQueryable<Invitation> ProjectsInvitations(IQueryable<Invitation> entities, string projectid)
        {
            IQueryable<Group> groups = GroupRepository.ProjectGroups(dbContext.Groups, projectid);
            entities = entities.Join(groups, i => i.GroupId, g => g.Id, (i,g)=> i);
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
            if (filterQuery.Has(PROJECT_LIST))
            {
                return ProjectsInvitations(entities, filterQuery.Value);
            }
            if (filterQuery.Has("email"))
            {
                string currentEmail = CurrentUser.Email.ToLower();
                return entities.Where(i => currentEmail == i.Email.ToLower());
            }
            return base.Filter(entities, filterQuery);
        }
    }
}
