using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using JsonApiDotNetCore.Configuration;

using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Queries.Internal;

namespace SIL.Transcriber.Services
{
    public class PlanService : BaseArchiveService<Plan>
    {
        private readonly PlanRepository MyRepository;
        private readonly IResourceRepositoryAccessor RepositoryAccessor;
        private readonly IEnumerable<IQueryConstraintProvider> ConstraintProviders;
        public PlanService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<Plan> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, PlanRepository myRepository,
            IEnumerable<IQueryConstraintProvider> constraintProviders
)           : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor)
        {
            RepositoryAccessor = repositoryAccessor;
            MyRepository = myRepository;
            ConstraintProviders = constraintProviders;
        }
        
        public Plan Get(int id)
        {
            return GetAsync(id, new CancellationToken()).Result;
        }

    }
}