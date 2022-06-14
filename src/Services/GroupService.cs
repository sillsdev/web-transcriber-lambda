using JsonApiDotNetCore.Configuration;
using SIL.Transcriber.Models;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class GroupService : BaseArchiveService<Group>
    {
        public GroupService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Group> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            GroupRepository repository
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                repository
            ) { }
    }
}
