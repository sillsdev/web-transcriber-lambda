using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
namespace SIL.Transcriber.Repositories
{
    public class FileResponseRepository : AppDbContextRepository<FileResponse>
    {
        public FileResponseRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor
        ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
            constraintProviders, loggerFactory, resourceDefinitionAccessor)
        {
        }
    }
}