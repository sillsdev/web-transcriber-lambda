using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
namespace SIL.Transcriber.Repositories
{
    public class FileresponseRepository : AppDbContextRepository<Fileresponse>
    {
        public FileresponseRepository(
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