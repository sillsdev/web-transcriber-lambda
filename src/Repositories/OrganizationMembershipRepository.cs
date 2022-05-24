using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using System.Linq;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationMembershipRepository : BaseRepository<OrganizationMembership>
    {
        readonly private OrganizationRepository OrganizationRepository;
        public OrganizationMembershipRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository organizationRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<OrganizationMembership> UsersOrganizationMemberships(IQueryable<OrganizationMembership> entities)
        {
            if (CurrentUser == null) return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                //if I'm an admin in the org, give me all oms in that org
                //otherwise give me just the oms I'm a member of
                IEnumerable<int> orgadmins = orgIds.Where(o => CurrentUser.HasOrgRole(RoleName.Admin, o));
                entities = entities.Where(om => orgadmins.Contains(om.OrganizationId) || om.UserId == CurrentUser.Id);
            }                
            return entities;
        }
        public IQueryable<OrganizationMembership> ProjectOrganizationMemberships(IQueryable<OrganizationMembership> entities, string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(dbContext.Organizations, projectid);
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }

        #region overrides
        public override IQueryable<OrganizationMembership> FromCurrentUser(IQueryable<OrganizationMembership>? entities = null)
        {
            return UsersOrganizationMemberships(entities ?? GetAll());
        }
        protected override IQueryable<OrganizationMembership> FromProjectList(IQueryable<OrganizationMembership>? entities, string idList)
        {
            return ProjectOrganizationMemberships(entities??GetAll(), idList);
        }
        #endregion
    }
}
