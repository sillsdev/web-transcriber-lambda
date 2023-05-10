using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

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
            )
        { }

        public override IQueryable<Resource> FromProjectList(
            IQueryable<Resource>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Resource> FromCurrentUser(IQueryable<Resource>? entities = null)
        {
            //todo filter out those with clusterids not in my org list
            return entities ?? GetAll();
        }
    }
}
