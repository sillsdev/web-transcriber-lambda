using System.Collections.Generic;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;

using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class ProjectIntegrationService : BaseService<ProjectIntegration>
    {
        public ProjectIntegrationService(
           IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<ProjectIntegration> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
        }
        public override async Task<IReadOnlyCollection<ProjectIntegration>> GetAsync(CancellationToken cancellationToken)
        {
            return await base.GetAsync(cancellationToken);
            //TODO return await GetScopedToCurrentUser(base.GetAsync,options);
        }

    }

}
