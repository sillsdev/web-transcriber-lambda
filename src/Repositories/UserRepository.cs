using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;

namespace SIL.Transcriber.Repositories
{
    public class UserRepository : BaseRepository<User>
    {
        private ICurrentUserContext CurrentUserContext { get; }
        readonly private OrganizationMembershipRepository OrgMemRepository;
        public UserRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            CurrentUserRepository currentUserRepository,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            ICurrentUserContext currentUserContext,
            OrganizationMembershipRepository orgmemRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory,
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
            CurrentUserContext = currentUserContext;
            OrgMemRepository = orgmemRepository;
        }
         
        
        public IQueryable<User> OrgMemUsers(IQueryable<User> entities, IQueryable<OrganizationMembership> orgmems)
        {
            var mems =  entities.Join(orgmems, u => u.Id, om => om.UserId, (u, om) => u);
            //add groupby and select because once we join with om, we duplicate users
            return mems.ToList().GroupBy(u => u.Id).Select(g => g.First()).AsAsyncQueryable();
        }
        
        public IQueryable<User> UsersUsers(IQueryable<User> entities)
        {
            if (CurrentUser == null) return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds;
                //always give me all users in that org
                entities = OrgMemUsers(entities, dbContext.Organizationmemberships.Where(om => orgIds.Contains(om.OrganizationId)));
            }
            return entities;
        }
        
        public IQueryable<User> ProjectUsers(IQueryable<User> entities, string projectid)
        {
            IQueryable<OrganizationMembership> orgmems = OrgMemRepository.ProjectOrganizationMemberships(dbContext.Organizationmemberships, projectid);
            return OrgMemUsers(entities, orgmems);
        }
        
        #region Overrides
        protected override IQueryable<User> GetAll()
        {
            return FromCurrentUser();
        }

        protected override IQueryable<User> FromCurrentUser(QueryLayer? layer = null)
        {
            return UsersUsers(base.GetAll().Where(u => !u.Archived));
        }

        protected override IQueryable<User> FromProjectList(QueryLayer layer, string idList)
        {
            return ProjectUsers(base.GetAll(), idList);
        }

        #endregion

        public void Refresh(User u)
        {
            User? local = dbContext.Set<User>().Local.FirstOrDefault(entry => entry.Id.Equals(u.Id));

            // check if local is not null 
            if (local != null)
            {
                dbContext.Entry(local).State = EntityState.Detached;
            }
            else
            {
                local = u;
            }
            local.LastModifiedOrigin = "refresh";  //this will get overwritten but just change something to force a save
            dbContext.Update(local);
            dbContext.SaveChanges();
        }
    }
}
