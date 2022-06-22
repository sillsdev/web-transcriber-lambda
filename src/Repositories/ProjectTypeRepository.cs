using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Repositories
{
    public class ProjectTypeRepository : BaseRepository<Projecttype>
    {
        public ProjectTypeRepository(
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

        public override IQueryable<Projecttype> FromCurrentUser(
            IQueryable<Projecttype>? entities = null
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Projecttype> FromProjectList(
            IQueryable<Projecttype>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
