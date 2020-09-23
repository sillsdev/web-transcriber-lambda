using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Transcriber.Data;

namespace SIL.Logging.Repositories
{
    public class ParatextTokenHistoryRepository : LoggingDbContextRepository<ParatextTokenHistory>
    {
        public ParatextTokenHistoryRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            LoggingDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, contextResolver)
        {
        }
    }
}