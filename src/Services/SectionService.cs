using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;

namespace SIL.Transcriber.Services
{
    public class SectionService : BaseArchiveService<Section>
    {
        private readonly SectionRepository MyRepository;

        public SectionService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Section> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            SectionRepository repository
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                repository
            )
        {
            MyRepository = repository;
        }

        public int? GetProjectId(int sectionId)
        {
            Section? section = MyRepository
                .Get()
                .Where(s => s.Id == sectionId)
                .Include(s => s.Plan)
                .FirstOrDefault();
            return section?.Plan?.ProjectId;
        }

        public IEnumerable<SectionSummary> GetSectionSummary(int PlanId, string book, int chapter)
        {
            return MyRepository.SectionSummary(PlanId, book, chapter).Result;
        }
    }
}
