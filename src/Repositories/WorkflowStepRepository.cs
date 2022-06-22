using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using System.Linq;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using System.Collections.Generic;

namespace SIL.Transcriber.Repositories
{
    public class WorkflowStepRepository : BaseRepository<Workflowstep>
    {
        public WorkflowStepRepository(
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

        #region Overrides
        public override IQueryable<Workflowstep> FromProjectList(
            IQueryable<Workflowstep>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }

        public override IQueryable<Workflowstep> FromCurrentUser(
            IQueryable<Workflowstep>? entities = null
        )
        {
            return entities ?? GetAll();
        }
        #endregion
    }
}
