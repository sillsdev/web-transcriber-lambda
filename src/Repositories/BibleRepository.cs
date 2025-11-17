using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class BibleRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository organizationRepository,
        MediafileRepository mediafileRepository
        ) : BaseRepository<Bible>(
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
        readonly private MediafileRepository MediafileRepository = mediafileRepository;

        private async Task PublishTitles(Bible bible)
        {
            if (bible.IsoMediafile != null)
                await MediafileRepository.Publish((int)bible.IsoMediafile.Id, "{\"Public\": \"true\"}", bible);
            if (bible.BibleMediafile != null)
                await MediafileRepository.Publish((int)bible.BibleMediafile.Id, "{\"Public\": \"true\"}", bible);
        }
        public override async Task CreateAsync(Bible resourceFromRequest, Bible resourceFromDatabase, CancellationToken cancellationToken)
        {
            await PublishTitles(resourceFromRequest);
            await base.CreateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
        }
        public override async Task UpdateAsync(Bible resourceFromRequest, Bible resourceFromDatabase, CancellationToken cancellationToken)
        {
            await PublishTitles(resourceFromRequest);
            await base.UpdateAsync(resourceFromRequest, resourceFromDatabase, cancellationToken);
        }

        public IQueryable<Bible> UsersBibles(
            IQueryable<Bible> entities)
        {
            return entities;
        }

        public IQueryable<Bible> ProjectBibles(
            IQueryable<Bible> entities,
            string projectid)
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
