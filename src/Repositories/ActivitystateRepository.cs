using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;

namespace SIL.Transcriber.Repositories
{
    public class ActivitystateRepository : BaseRepository<Activitystate>
    {
        public ActivitystateRepository(
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
