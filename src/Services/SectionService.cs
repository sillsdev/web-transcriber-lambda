using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class SectionService : BaseArchiveService<Section>
    {
        public IOrganizationContext OrganizationContext { get; private set; }

        public SectionService(
            IJsonApiContext jsonApiContext,
            IOrganizationContext organizationContext,
            IEntityRepository<Section> sectionRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, sectionRepository, loggerFactory)
        {
            OrganizationContext = organizationContext;
        }
        public override async Task<IEnumerable<Section>> GetAsync()
        {
            return await GetScopedToOrganization<Section>(
                base.GetAsync,
                OrganizationContext,
                JsonApiContext);

        }

        public override async Task<Section> GetAsync(int id)
        {
            var sections = await GetAsync();

            return sections.SingleOrDefault(g => g.Id == id);
        }
    }
}