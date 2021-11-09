using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class ArtifactCategoryService : BaseArchiveService<ArtifactCategory>
    {
        public ArtifactCategoryService(
            IJsonApiContext jsonApiContext,
            ArtifactCategoryRepository ArtifactCategoryRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, ArtifactCategoryRepository, loggerFactory)
        {
        }
    }
}
