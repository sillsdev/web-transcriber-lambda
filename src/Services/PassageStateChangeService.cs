using System.Collections.Generic;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PassageStateChangeService : BaseService<PassageStateChange>
    {

        public PassageStateChangeService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<PassageStateChange> repository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, repository, loggerFactory)
        {
        }
        public override async Task<IEnumerable<PassageStateChange>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }
    }
    
}
