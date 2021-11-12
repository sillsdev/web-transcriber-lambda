using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class ArtifactTypeService : BaseArchiveService<ArtifactType>
    {
        public ArtifactTypeService(
            IJsonApiContext jsonApiContext,

            ArtifactTypeRepository ArtifactTypeRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, ArtifactTypeRepository, loggerFactory)
        {

        }
        public override async Task<IEnumerable<ArtifactType>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<ArtifactType> GetAsync(int id)
        {
            IEnumerable<ArtifactType> Workflows = await GetAsync();

            return Workflows.SingleOrDefault(g => g.Id == id);
        }
    }
}
