using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Repositories
{
    public class IntellectualPropertyRepository : BaseRepository<Intellectualproperty>
    {
        private readonly OrganizationRepository OrganizationRepository;

        public IntellectualPropertyRepository(
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
        {
            OrganizationRepository = organizationRepository;
        }
        public IQueryable<Intellectualproperty> UsersIntellectualProperty(IQueryable<Intellectualproperty> entities)
        {
            if (CurrentUser == null)
                return entities.Where(e => e.Id == -1);
            IEnumerable<int> orgIds = CurrentUser.OrganizationIds.OrEmpty();
            return entities.Where(om => orgIds.Contains(om.OrganizationId));
        }
        public override IQueryable<Intellectualproperty> FromCurrentUser(
            IQueryable<Intellectualproperty>? entities = null
        )
        {
            return UsersIntellectualProperty(entities ?? GetAll());
        }

        public IQueryable<Intellectualproperty> ProjectIntellectualProperty(IQueryable<Intellectualproperty> entities,
                                                                            string projectid)
        {
            IQueryable<Organization> orgs = OrganizationRepository.ProjectOrganizations(
                dbContext.Organizations,
                projectid
            );
            return entities.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
        }
        public override IQueryable<Intellectualproperty> FromProjectList(
            IQueryable<Intellectualproperty>? entities,
            string idList
        )
        {
            return ProjectIntellectualProperty(entities ?? GetAll(), idList);
        }
    }
}
