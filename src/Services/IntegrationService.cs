
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class IntegrationService : BaseArchiveService<Integration>
    {

        public IntegrationService(
            IJsonApiContext jsonApiContext,
            IntegrationRepository myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
        }
    }
}
