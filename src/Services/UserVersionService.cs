using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class UserVersionService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Userversion> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        UserVersionRepository repository
        ) : BaseService<Userversion>(
            repositoryAccessor,
            queryLayerComposer,
            paginationContext,
            options,
            loggerFactory,
            request,
            resourceChangeTracker,
            resourceDefinitionAccessor,
            repository
            )
    {
        public Userversion StoreVersion(string version)
        {
            return StoreVersion(version, "unknown");
        }

        public Userversion StoreVersion(string version, string env)
        {
            return ((UserVersionRepository)Repo).CreateOrUpdate(version, env);
        }
    }
}
