
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;

namespace SIL.Transcriber.Services
{
    public class IntegrationService : BaseArchiveService<Integration>
    {

        public IntegrationService(
            IJsonApiContext jsonApiContext,
            AppDbContextRepository<Integration> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
        }
    }
}
