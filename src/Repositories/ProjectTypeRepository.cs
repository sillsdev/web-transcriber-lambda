using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Repositories
{
    public class ProjectTypeRepository : BaseRepository<ProjectType>
    {
        public ProjectTypeRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository
            ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory,
                constraintProviders, loggerFactory, resourceDefinitionAccessor, currentUserRepository)
        {
        }
        protected override IQueryable<ProjectType> FromCurrentUser(QueryLayer? layer = null)
        {
            return GetAll();
        }
        protected override IQueryable<ProjectType> FromProjectList(QueryLayer layer, string idList)
        {
            return GetAll();
        }
    }
}