using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Paratext.Models;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class ParatextTokenRepository : AppDbContextRepository<ParatextToken>
    {
        public ParatextTokenRepository(
            ILoggerFactory loggerFactory,
            IJsonApiContext jsonApiContext,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
            ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
    }
}