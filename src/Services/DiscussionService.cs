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
    public class DiscussionService : BaseArchiveService<Discussion>
    {
        public DiscussionService(
            IJsonApiContext jsonApiContext,

            DiscussionRepository DiscussionRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, DiscussionRepository, loggerFactory)
        {

        }
        public override async Task<IEnumerable<Discussion>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<Discussion> GetAsync(int id)
        {
            IEnumerable<Discussion> Workflows = await GetAsync();

            return Workflows.SingleOrDefault(g => g.Id == id);
        }
    }
}
