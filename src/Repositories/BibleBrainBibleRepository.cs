using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class BibleBrainBibleRepository : BaseRepository<Biblebrainbible>
    { 
        public BibleBrainBibleRepository(
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
        {  }

        public IQueryable<Biblebrainbible> UsersBibles(
            IQueryable<Biblebrainbible> entities
        )
        {
            return entities;
        }

        public IQueryable<Biblebrainbible> ProjectBibles(
            IQueryable<Biblebrainbible> entities,
            string projectid
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
