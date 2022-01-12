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
    public class CommentService : BaseArchiveService<Comment>
    {
        public CommentService(
            IJsonApiContext jsonApiContext,

            CommentRepository CommentRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, CommentRepository, loggerFactory)
        {

        }
        public override async Task<IEnumerable<Comment>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<Comment> GetAsync(int id)
        {
            IEnumerable<Comment> Workflows = await GetAsync();

            return Workflows.SingleOrDefault(g => g.Id == id);
        }
    }
}
