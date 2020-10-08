using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class ParatextSyncPassageRepository : LoggingDbContextRepository<ParatextSyncPassage>
    {
        public ParatextSyncPassageRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            LoggingDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
        }
    }
}