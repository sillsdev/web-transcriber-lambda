using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.IEnumerableExtensions;

namespace SIL.Transcriber.Repositories
{
    public class GraphicRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Graphic>(
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
        private readonly OrganizationRepository OrganizationRepository = organizationRepository;

        public IQueryable<Graphic> UsersGraphics(
            IQueryable<Graphic> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                entities = entities.Where(g => !g.Archived && orgIds.Contains(g.OrganizationId));
            }
            return entities.Where(e => !e.Archived);
        }

        public IQueryable<Graphic> ProjectGraphics(
            IQueryable<Graphic> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            return entities.Join(orgs, g => g.OrganizationId, o => o.Id, (g, o) => g).Where(g => !g.Archived);
        }

        #region Overrides
        public override IQueryable<Graphic> FromProjectList(
            IQueryable<Graphic>? entities,
            string idList
        )
        {
            return ProjectGraphics(entities ?? GetAll(), idList);
        }

        public override IQueryable<Graphic> FromCurrentUser(
            IQueryable<Graphic>? entities = null
        )
        {
            return UsersGraphics(entities ?? GetAll());
        }
        #endregion
    }
}
