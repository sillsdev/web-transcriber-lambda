using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class RoleRepository : BaseRepository<Role>
    {
        public RoleRepository(
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

        public override IQueryable<Role> FromCurrentUser(IQueryable<Role>? entities = null)
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Role> FromProjectList(IQueryable<Role>? entities, string idList)
        {
            return entities ?? GetAll();
        }
    }
}
