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
    public class OrgArtifactTypeService : BaseArchiveService<OrgArtifactType>
    {
        public OrgArtifactTypeService(
            IJsonApiContext jsonApiContext,

            OrgArtifactTypeRepository OrgArtifactTypeRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, OrgArtifactTypeRepository, loggerFactory)
        {

        }
        public override async Task<IEnumerable<OrgArtifactType>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<OrgArtifactType> GetAsync(int id)
        {
            IEnumerable<OrgArtifactType> Workflows = await GetAsync();

            return Workflows.SingleOrDefault(g => g.Id == id);
        }
    }
}
