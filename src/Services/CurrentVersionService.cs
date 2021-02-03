using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class CurrentVersionService : BaseService<CurrentVersion>
    {
        public CurrentVersionService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            CurrentVersionRepository repository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, repository, loggerFactory)
        {
        }

        public CurrentVersion StoreVersion(string version)
        {
            return ((CurrentVersionRepository)MyRepository).CreateOrUpdate(version);
        }
    }
}
