using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class BookRepository : BaseRepository<Book>
    {
        public BookRepository(
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
        {
        }

        #region overrides
        public override IQueryable<Book> FromProjectList(
            IQueryable<Book>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Book> FromCurrentUser(IQueryable<Book>? entities = null)
        {
            return entities ?? GetAll();
        }
        #endregion
    }
}
