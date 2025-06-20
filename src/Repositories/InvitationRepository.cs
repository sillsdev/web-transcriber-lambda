﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class InvitationRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        GroupRepository groupRepository
        ) : BaseRepository<Invitation>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        readonly private GroupRepository GroupRepository = groupRepository;

        private IQueryable<Invitation> UsersInvitations(IQueryable<Invitation> entities)
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //if I'm an admin in the org, give me all invitations in that org
                //otherwise give me invitations just to me
                //no-that was confusing...
                //IEnumerable<int> orgadmins = orgIds.Where(
                //    o => CurrentUser.HasOrgRole(RoleName.Admin, o)
                //);
                string currentEmail = CurrentUser.Email?.ToLower() ?? "";

#pragma warning disable CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
                //DO NOT use StringComparison, because this is a query being sent to database and it throws
                entities = entities.Where(i =>
                        orgIds.Contains(i.OrganizationId)
                        || currentEmail == (i.Email ?? "").ToLower()

                );
#pragma warning restore CA1862 // Use the 'StringComparison' method overloads to perform case-insensitive string comparisons
            }
            return entities;
        }

        private IQueryable<Invitation> ProjectsInvitations(
            IQueryable<Invitation> entities,
            string projectid
        )
        {
            IQueryable<Group> groups = GroupRepository.ProjectGroups(dbContext.Groups, projectid);
            entities = entities.Join(groups, i => i.GroupId, g => g.Id, (i, g) => i);
            return entities;
        }

        public override IQueryable<Invitation> FromCurrentUser(
            IQueryable<Invitation>? entities = null
        )
        {
            return UsersInvitations(entities ?? GetAll());
        }

        public override IQueryable<Invitation> FromProjectList(
            IQueryable<Invitation>? entities,
            string idList
        )
        {
            return ProjectsInvitations(entities ?? GetAll(), idList);
        }
        /* //TODO???
            if (filterQuery.Has("email"))
            {
                string currentEmail = CurrentUser.Email.ToLower();
                return entities.Where(i => currentEmail == i.Email.ToLower());
            }
            return base.Filter(entities, filterQuery);
        }*/
    }
}
