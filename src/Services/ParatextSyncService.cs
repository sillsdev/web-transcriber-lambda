using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Logging.Repositories;

namespace SIL.Transcriber.Services
{
    public class ParatextSyncService : EntityResourceService<ParatextSync>
    {
        public ParatextSyncService(IJsonApiContext jsonApiContext,
            ParatextSyncRepository tokenRepository,
        ILoggerFactory loggerFactory)
    : base(jsonApiContext, tokenRepository, loggerFactory)
        {
        }
    }
}
