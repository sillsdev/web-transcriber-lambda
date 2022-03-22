using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class SectionResourceService : BaseArchiveService<SectionResource>
    {
        public SectionResourceService(
            IJsonApiContext jsonApiContext,
           SectionResourceRepository SectionResourceRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, SectionResourceRepository, loggerFactory)
        {
        }
        public override async Task<IEnumerable<SectionResource>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }

        public override async Task<SectionResource> GetAsync(int id)
        {
            IEnumerable<SectionResource> SectionResources = await GetAsync();

            return SectionResources.SingleOrDefault(g => g.Id == id);
        }
    }
}