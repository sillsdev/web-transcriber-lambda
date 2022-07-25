using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.IEnumerableExtensions;

namespace SIL.Transcriber.Repositories
{
    public class GroupRepository : BaseRepository<Group>
    {
        public GroupRepository(
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
                //if I'm an admin in the org, give me all groups in that org
                //otherwise give me just the groups I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(
                    o => CurrentUser.HasOrgRole(RoleName.Admin, o)
                );

                return entities.Where(
                    g => orgadmins.Contains(g.OwnerId) || CurrentUser.GroupIds.Contains(g.Id)
                );
            }
            return entities;
        }

        public IQueryable<Group> ProjectGroups(IQueryable<Group> entities, string projectid)
        {
            IQueryable<Project> projects = dbContext.Projects.Where(
                p => p.Id.ToString() == projectid
            );
            return entities.Join(projects, g => g.Id, p => p.GroupId, (g, p) => g);
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
