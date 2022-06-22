using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;

namespace SIL.Transcriber.Repositories
{
    public class ResourceRepository : BaseRepository<Resource>
    {
        public ResourceRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
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
            ) { }

        public override IQueryable<Resource> FromProjectList(
            IQueryable<Resource>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Resource> FromCurrentUser(IQueryable<Resource>? entities = null)
        {
            return entities ?? GetAll();
        }
    }
}
