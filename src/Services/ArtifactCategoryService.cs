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
    public class ArtifactCategoryService : BaseArchiveService<ArtifactCategory>
    {
        public ArtifactCategoryService(
            IJsonApiContext jsonApiContext,

            ArtifactCategoryRepository ArtifactCategoryRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, ArtifactCategoryRepository, loggerFactory)
        {

        }
        public override async Task<IEnumerable<ArtifactCategory>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<ArtifactCategory> GetAsync(int id)
        {
            IEnumerable<ArtifactCategory> Workflows = await GetAsync();

            return Workflows.SingleOrDefault(g => g.Id == id);
        }
    }
}
