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
    public class PassageService : BaseArchiveService<Passage>
    {
        private readonly HttpContext? HttpContext;
        private readonly PassageRepository MyRepository;
        public PassageService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<Passage> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            IHttpContextAccessor httpContextAccessor,
            PassageRepository myRepository) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request, resourceChangeTracker, resourceDefinitionAccessor,myRepository)
        {
            HttpContext = httpContextAccessor.HttpContext;
            MyRepository = myRepository;
        }

        public IQueryable<Passage> GetBySection(int SectionId)
        {
            return MyRepository.Get()
                    .Include(p => p.Section)
                    .Where(p => p.SectionId == SectionId);
        }
        public IQueryable<Passage> Get(int id)
        {
            return MyRepository.Get().Include(p => p.Section).Where(p => p.Id == id);
        }
        public int? GetProjectId(Passage passage)
        {
            return MyRepository.ProjectId(passage);
        }
        public IQueryable<Passage> ReadyToSync(int PlanId)
        {
            return MyRepository.ReadyToSync(PlanId);
        }

    }
}