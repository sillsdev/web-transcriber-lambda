using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class SectionService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Section> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        SectionRepository repository
        ) : BaseArchiveService<Section>(
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
        private readonly SectionRepository MyRepository = repository;

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
        public IEnumerable<Section> AssignSections(int scheme, string idlist)
        {
            return MyRepository.AssignSections(scheme, idlist);
        }
    }
}
