using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
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
    }
}