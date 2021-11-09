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
    public class OrgArtifactCategoryService : BaseArchiveService<OrgArtifactCategory>
    {
        public OrgArtifactCategoryService(
            IJsonApiContext jsonApiContext,

            OrgArtifactCategoryRepository OrgArtifactCategoryRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, OrgArtifactCategoryRepository, loggerFactory)
        {

        }
        public override async Task<IEnumerable<OrgArtifactCategory>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<OrgArtifactCategory> GetAsync(int id)
        {
            IEnumerable<OrgArtifactCategory> Workflows = await GetAsync();

            return Workflows.SingleOrDefault(g => g.Id == id);
        }
    }
}
