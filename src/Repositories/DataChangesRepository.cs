using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class DatachangesRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Datachanges>(
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
        public override IQueryable<Datachanges> FromCurrentUser(
            IQueryable<Datachanges>? entities = null
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Datachanges> FromProjectList(
            IQueryable<Datachanges>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
