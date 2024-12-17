using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationBibleRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Organizationbible>(
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
        readonly private OrganizationRepository OrganizationRepository = organizationRepository;

        public IQueryable<Organizationbible> UsersOrganizationBibles(
            IQueryable<Organizationbible> entities
        )
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                //if I'm an admin in the org, give me all oms in that org
                //otherwise give me just the oms I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(
                    o => CurrentUser.HasOrgRole(RoleName.Admin, o)
                );
                entities = entities.Where(om => orgIds.Contains(om.OrganizationId));
            }
            return entities;
        }

        public IQueryable<Organizationbible> ProjectOrganizationBibles(
            IQueryable<Organizationbible> entities,
            string projectid
        )
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region overrides
        public override IQueryable<Organizationbible> FromCurrentUser(
            IQueryable<Organizationbible>? entities = null
        )
        {
            return UsersOrganizationBibles(entities ?? GetAll());
        }

        public override IQueryable<Organizationbible> FromProjectList(
            IQueryable<Organizationbible>? entities,
            string idList
        )
        {
            return ProjectOrganizationBibles(entities ?? GetAll(), idList);
        }
        #endregion
    }
}
