using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class BibleBrainSectionRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Biblebrainsection>(
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
        public IQueryable<Biblebrainsection> UsersBibles(
            IQueryable<Biblebrainsection> entities
        )
        {
            return entities;
        }

        public IQueryable<Biblebrainsection> ProjectBibles(
            IQueryable<Biblebrainsection> entities,
            string projectid
        )
        {
            return entities;
        }

        #region Overrides
        public override IQueryable<Biblebrainsection> FromProjectList(
            IQueryable<Biblebrainsection>? entities,
            string idList
        )
        {
            return ProjectBibles(entities ?? GetAll(), idList);
        }

        public override IQueryable<Biblebrainsection> FromCurrentUser(
            IQueryable<Biblebrainsection>? entities = null
        )
        {
            return UsersBibles(entities ?? GetAll());
        }
        #endregion

    }
}
