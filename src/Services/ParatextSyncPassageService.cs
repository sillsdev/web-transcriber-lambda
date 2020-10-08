using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class ParatextSyncPassageService : EntityResourceService<ParatextSyncPassage>
    {
        public ParatextSyncPassageService(IJsonApiContext jsonApiContext,
            ParatextSyncPassageRepository tokenRepository,
        ILoggerFactory loggerFactory)
    : base(jsonApiContext, tokenRepository, loggerFactory)
        {
        }
    }
}