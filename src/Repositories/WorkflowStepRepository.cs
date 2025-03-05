using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Repositories
{
    public class WorkflowStepRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository
        ) : BaseRepository<Workflowstep>(
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

        #region Overrides
        public override IQueryable<Workflowstep> FromProjectList(
            IQueryable<Workflowstep>? entities,
            string idList
        )
        {
            return (entities ?? GetAll())   ;
        }

        public override IQueryable<Workflowstep> FromCurrentUser(
            IQueryable<Workflowstep>? entities = null
        )
        {
            return (entities ?? GetAll());
        }
        #endregion
    }
}
