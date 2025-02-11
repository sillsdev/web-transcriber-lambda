using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class BibleBrainBibleRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Biblebrainbible>(
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
        public IQueryable<Biblebrainbible> UsersBibles(
            IQueryable<Biblebrainbible> entities
        )
        {
            return entities;
        }

        public IQueryable<Biblebrainbible> ProjectBibles(
            IQueryable<Biblebrainbible> entities,
#pragma warning disable IDE0060 // Remove unused parameter
            string projectid
#pragma warning restore IDE0060 // Remove unused parameter
        )
        {
            return entities;
        }

        #region Overrides

        public override IQueryable<Biblebrainbible> FromProjectList(
            IQueryable<Biblebrainbible>? entities,
            string idList
        )
        {
            return ProjectBibles(entities ?? GetAll(), idList);
        }

        public override IQueryable<Biblebrainbible> FromCurrentUser(
            IQueryable<Biblebrainbible>? entities = null
        )
        {
            return UsersBibles(entities ?? GetAll());
        }
        #endregion
    }
}
