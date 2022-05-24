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
        public override IQueryable<ProjectType> FromCurrentUser(IQueryable<ProjectType>? entities = null)
        {
            return entities ?? GetAll();
        }
        protected override IQueryable<ProjectType> FromProjectList(IQueryable<ProjectType>? entities, string idList)
        {
            return entities ?? GetAll();
        }
    }
}