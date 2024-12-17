using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationMembershipRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository
        ) : BaseRepository<Organizationmembership>(
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

        public IQueryable<Organizationmembership> UsersOrganizationMemberships(
            IQueryable<Organizationmembership> entities
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
                entities = entities.Where(
                    om => (orgadmins.Contains(om.OrganizationId) || om.UserId == CurrentUser.Id)
                );
            }
            return entities;
        }

        public IQueryable<Organizationmembership> ProjectOrganizationMemberships(
            IQueryable<Organizationmembership> entities,
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
        public override IQueryable<Organizationmembership> FromCurrentUser(
            IQueryable<Organizationmembership>? entities = null
        )
        {
            return UsersOrganizationMemberships(entities ?? GetAll());
        }

        public override IQueryable<Organizationmembership> FromProjectList(
            IQueryable<Organizationmembership>? entities,
            string idList
        )
        {
            return ProjectOrganizationMemberships(entities ?? GetAll(), idList);
        }
        #endregion
    }
}
