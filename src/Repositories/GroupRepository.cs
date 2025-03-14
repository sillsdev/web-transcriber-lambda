﻿using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.IEnumerableExtensions;

namespace SIL.Transcriber.Repositories
{
    public class GroupRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Group>(
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
        public IQueryable<Group> GetMine()
        {
            return FromCurrentUser().Include(g => g.Owner);
        }
        public IQueryable<Group> UsersGroups(IQueryable<Group> entities)
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();

                return entities.Where(
                    g =>   (orgIds.Contains(g.OwnerId) || CurrentUser.GroupIds.Contains(g.Id) || g.Name == "All users of BibleMedia")
                );
            }
            return entities;
        }

        public IQueryable<Group> ProjectGroups(IQueryable<Group> entities, string projectid)
        {
            IQueryable<Project> projects = dbContext.Projects.Where(
                p => p.Id.ToString() == projectid
            );
            int orgId = projects.FirstOrDefault()?.OrganizationId ?? 0;
            
            return entities.Where(g => g.OwnerId == orgId);
        }

        public override IQueryable<Group> FromCurrentUser(IQueryable<Group>? entities = null)
        {
            return UsersGroups(entities ?? GetAll());
        }

        public override IQueryable<Group> FromProjectList(
            IQueryable<Group>? entities,
            string idList
        )
        {
            return ProjectGroups(entities ?? GetAll(), idList);
        }
    }
}
