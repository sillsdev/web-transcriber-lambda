using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class UserVersionService : BaseService<UserVersion>
    {
        public UserVersionRepository MyRepository;
        public UserVersionService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<UserVersion> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor, UserVersionRepository myRepository) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor)
        {
            MyRepository = myRepository;
        }
        public UserVersion StoreVersion(string version)
        {
            return StoreVersion(version, "unknown");
        }
        public UserVersion StoreVersion(string version, string env)
        {
            return MyRepository.CreateOrUpdate(version, env);
        }
    }
}
