using JsonApiDotNetCore.Configuration;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Services
{
        public class ActivitystateService : BaseService<Activitystate>
        {

            public ActivitystateService(
                IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
                IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
                IJsonApiRequest request, IResourceChangeTracker<Activitystate> resourceChangeTracker,
                IResourceDefinitionAccessor resourceDefinitionAccessor) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
            {
            }   
        }
}
