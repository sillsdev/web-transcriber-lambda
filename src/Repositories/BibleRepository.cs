using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.IEnumerableExtensions;

namespace SIL.Transcriber.Repositories
{
    public class BibleRepository : BaseRepository<Bible>
    {
        private readonly OrganizationRepository OrganizationRepository;
        public BibleRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository
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
        { OrganizationRepository = organizationRepository; }

        public IQueryable<Bible> UsersBibles(
            IQueryable<Bible> entities
        )
        {
            /*
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                //entities = entities.Join(dbContext.Organizations.Where(o => !o.Archived && orgIds.Contains(o.Id)), b => b.Id, o => o.BibleId, (b, o) => b);
            }
            */
            return entities;
        }

        public IQueryable<Bible> ProjectBibles(
            IQueryable<Bible> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            return entities
                //.Join(orgs, b => b.Id, o => o.BibleId, (b, o) => b)
                .Where(b => !b.Archived);
        }

        #region Overrides
        public override IQueryable<Bible> FromProjectList(
            IQueryable<Bible>? entities,
            string idList
        )
        {
            return ProjectBibles(entities ?? GetAll(), idList);
        }

        public override IQueryable<Bible> FromCurrentUser(
            IQueryable<Bible>? entities = null
        )
        {
            return UsersBibles(entities ?? GetAll());
        }
        #endregion
    }
}
