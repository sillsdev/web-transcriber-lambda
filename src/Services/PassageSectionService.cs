using System.Collections.Generic;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PassageSectionService : BaseArchiveService<PassageSection>
    {

        public PassageSectionService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<PassageSection> PassageSectionRepository,
          ILoggerFactory loggerFactory) : base(jsonApiContext, PassageSectionRepository, loggerFactory)
        {
        }
        public override async Task<IEnumerable<PassageSection>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }
    }
}