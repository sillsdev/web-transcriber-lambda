using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;

namespace SIL.Transcriber.Repositories
{
    public class ResourceRepository : BaseRepository<Resource>
    {
        public ResourceRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, constraintProviders,
                loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
        }
        protected override IQueryable<Resource> FromProjectList(QueryLayer layer, string idList)
        {
            return GetAll();
        }
        protected override IQueryable<Resource> FromCurrentUser(QueryLayer layer)
        {
            return GetAll();
        }
    }
}