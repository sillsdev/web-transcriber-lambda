using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Logging.Models;
using SIL.Logging.Repositories;

namespace SIL.Transcriber.Services
{
    public class ParatextTokenHistoryService : EntityResourceService<ParatextTokenHistory>
    {
        public ParatextTokenHistoryService(IJsonApiContext jsonApiContext,
            ParatextTokenHistoryRepository tokenRepository,
            ILoggerFactory loggerFactory)
    : base(jsonApiContext, tokenRepository, loggerFactory)
        {
        }
    }
}