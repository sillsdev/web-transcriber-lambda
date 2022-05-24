using System.Linq;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using System;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using System.Collections.Generic;
using SIL.Transcriber.Utility;
using JsonApiDotNetCore.Serialization;
using Microsoft.EntityFrameworkCore;

namespace SIL.Transcriber.Repositories
{
    public class OrganizationRepository : BaseRepository<Organization>
    {
        private readonly ProjectRepository ProjectRepository;
        public OrganizationRepository(
                   ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            ProjectRepository projectRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
                constraintProviders, loggerFactory,resourceDefinitionAccessor, currentUserRepository)
        {
            ProjectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
        }
        public IQueryable<Organization> UsersOrganizations(IQueryable<Organization> entities)
        {
            if (CurrentUser == null) return entities.Where(e => e.Id == -1);

            if (!CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0))
            {
                IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
                return entities.Where(o => orgIds.Contains(o.Id));
            }
            return entities;
        }
        public IQueryable<Organization>ProjectOrganizations(IQueryable<Organization> entities, string projectid)
        {
            IQueryable<Project> projects = ProjectRepository.ProjectProjects(dbContext.Projects, projectid);
            return entities.Join(projects, o => o.Id, p => p.OrganizationId, (o, p) => o); //.GroupBy(o => o.Id).Select(g => g.First());
        }
        public IQueryable<Organization> GetMine()
        {
            return FromCurrentUser();
        }
        #region Overrides
        public override IQueryable<Organization> FromCurrentUser(IQueryable<Organization>? entities = null)
        {
            return UsersOrganizations(entities??GetAll());
        }
        protected override IQueryable<Organization> FromProjectList(IQueryable<Organization>? entities, string idList)
        {
            return ProjectOrganizations(entities??GetAll(), idList);
        }
        #endregion
    }
}
