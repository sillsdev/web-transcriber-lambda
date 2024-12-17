using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class ActivitystateRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Activitystate>(
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

        #region Overrides
        public override IQueryable<Activitystate> FromProjectList(
            IQueryable<Activitystate>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Activitystate> FromCurrentUser(
            IQueryable<Activitystate>? entities = null
        )
        {
            return entities ?? GetAll();
        }
        #endregion
    }
}
