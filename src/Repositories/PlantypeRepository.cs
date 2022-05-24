using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Repositories
{
    public class PlanTypeRepository : BaseRepository<PlanType>
    {
        public PlanTypeRepository(
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
        public override IQueryable<PlanType> FromCurrentUser(IQueryable<PlanType>? entities = null)
        {
            return entities ?? GetAll();
        }
        protected override IQueryable<PlanType> FromProjectList(IQueryable<PlanType>? entities, string idList)
        {
            return entities ?? GetAll();
        }
    }
}