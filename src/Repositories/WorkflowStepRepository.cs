using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using JsonApiDotNetCore.Internal.Query;
using static SIL.Transcriber.Utility.IEnumerableExtensions;
using static SIL.Transcriber.Utility.RepositoryExtensions;
using System.Linq;

namespace SIL.Transcriber.Repositories
{
    public class WorkflowStepRepository : BaseRepository<WorkflowStep>
    {
        public WorkflowStepRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
        #region Overrides
        public override IQueryable<WorkflowStep> Filter(IQueryable<WorkflowStep> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(ORGANIZATION_HEADER) || filterQuery.Has(ALLOWED_CURRENTUSER) || filterQuery.Has(PROJECT_LIST))
            {
                return entities;
            }
            return base.Filter(entities, filterQuery);
        }
        #endregion
    }
}