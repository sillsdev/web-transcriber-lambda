using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Configuration;

using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Services;

namespace SIL.Transcriber.Services
{
    public class FileResponseService : JsonApiResourceService<FileResponse, int>
    {
             public FileResponseService(
                IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<FileResponse> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor
            ) : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
            {
            }
        }
}
