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
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);

            /*return await GetScopedToOrganization<Section>(
                base.GetAsync,
                OrganizationContext,
                JsonApiContext);
            */
        }

        public override async Task<Section> GetAsync(int id)
        {
            var sections = await GetAsync();

            return sections.SingleOrDefault(g => g.Id == id);
        }

        public IEnumerable<Passage> AssignUser(int id, int userId, string role)
        {
            return ((SectionRepository)MyRepository).AssignUser(id, userId, role);
        }
        public IEnumerable<Passage> DeleteAssignment(int id, string role)
        {
            return ((SectionRepository)MyRepository).DeleteAssignment(id, role);
        }
        public IEnumerable<Assignment> GetAssignedUsers(int id)
        {
            //return ((SectionRepository)MyRepository).GetWithPassageAssignments(id);
            return ((SectionRepository)MyRepository).GetPassageAssignments(id);
        }

    }
}