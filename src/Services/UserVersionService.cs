using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class UserVersionService : BaseService<UserVersion>
    {
        public UserVersionService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            UserVersionRepository userversionRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, userversionRepository, loggerFactory)
        {
        }

        public UserVersion StoreVersion(string version)
        {
            return ((UserVersionRepository)MyRepository).CreateOrUpdate(version);
        }
    }
}
