using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class ArtifactTypeRepository : BaseRepository<ArtifactType>
    {
        public ArtifactTypeRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          AppDbContextResolver contextResolver
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
    }
}