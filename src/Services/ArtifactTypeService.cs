using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;


namespace SIL.Transcriber.Services
{
    public class ArtifactTypeService : BaseArchiveService<ArtifactType>
    {
        public ArtifactTypeService(
            IJsonApiContext jsonApiContext,
            ArtifactTypeRepository ArtifactTypeRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, ArtifactTypeRepository, loggerFactory)
        {
        }
    }
}
