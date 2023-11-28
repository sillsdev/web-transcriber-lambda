using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Services
{
    public class OrganizationService : BaseArchiveService<Organization>
    {
        public GroupMembershipService GroupMembershipService { get; }
        readonly private HttpContext? HttpContext;
        protected readonly AppDbContext dbContext;

        public OrganizationService(
            IHttpContextAccessor httpContextAccessor,
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Organization> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            OrganizationRepository repository,
            GroupMembershipService groupMembershipService,
            AppDbContextResolver contextResolver
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                repository
            )
        {
            HttpContext = httpContextAccessor.HttpContext;
            GroupMembershipService = groupMembershipService;
            dbContext = (AppDbContext)contextResolver.GetContext();
        }

        public bool AnyPublished(int id)
        {
            return ((OrganizationRepository)Repo).AnyPublished(id);
        }   
        public void JoinOrg(Organization entity, User user, RoleName orgRole, RoleName groupRole)
        {
            Group? allGroup = dbContext.Groups
                .Where(g => g.AllUsers && g.OwnerId == entity.Id)
                .FirstOrDefault();

            if (HttpContext != null)
                HttpContext.SetFP("api join org");
            Organizationmembership? membership = dbContext.Organizationmemberships
                .Where(om => om.OrganizationId == entity.Id && om.UserId == user.Id)
                .FirstOrDefault();
            if (user == null)
                return;
            if (membership == null)
            {
                membership = new Organizationmembership
                {
                    User = user,
                    UserId = user.Id,
                    Organization = entity,
                    OrganizationId = entity.Id,
                    RoleId = (int)orgRole
                };
                _ = dbContext.Organizationmemberships.Add(membership);
                //dbContext.SaveChanges();
            }
            else
            {
                if (membership.Archived || membership.RoleName != orgRole)
                {
                    membership.RoleId = (int)orgRole;
                    membership.Archived = false;
                    _ = dbContext.Organizationmemberships.Update(membership);
                    //dbContext.SaveChanges();
                }
            }
            if (allGroup != null)
            {
                _ = GroupMembershipService.JoinGroup(user.Id, allGroup.Id, groupRole);
            }
        }
    }
}
