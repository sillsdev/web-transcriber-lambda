using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class PassagetypeRepository : BaseRepository<Passagetype>
    {
        public PassagetypeRepository(
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

        public override IQueryable<Passagetype> FromCurrentUser(
            IQueryable<Passagetype>? entities = null
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Passagetype> FromProjectList(
            IQueryable<Passagetype>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
