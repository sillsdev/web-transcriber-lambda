using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PassageService : BaseArchiveService<Passage>
    {
        public IOrganizationContext OrganizationContext { get; private set; }

        public PassageService(
            IJsonApiContext jsonApiContext,
            IOrganizationContext organizationContext,
            IEntityRepository<Passage> PassageRepository,
           ILoggerFactory loggerFactory) : base(jsonApiContext, PassageRepository, loggerFactory)
        {
            OrganizationContext = organizationContext;
        }
        public override async Task<IEnumerable<Passage>> GetAsync()
        {
            return await GetScopedToOrganization<Passage>(
                base.GetAsync,
                OrganizationContext,
                JsonApiContext);
        }

        public override async Task<Passage> GetAsync(int id)
        {
            var passages = await GetAsync();

            return passages.SingleOrDefault(g => g.Id == id);
        }
    }
}