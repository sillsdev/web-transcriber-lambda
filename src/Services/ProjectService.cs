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
    public class ProjectService : BaseArchiveService<Project>
    {
        readonly ProjectRepository MyRepository;

        public ProjectService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Project> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            ProjectRepository myRepository
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
                myRepository
            )
        {
            MyRepository = myRepository;
        }

        public async Task<Project?> GetWithPlansAsync(int id)
        {
            return await MyRepository
                .Get()
                .Where(p => p.Id == id)
                .Include(p => p.Plans)
                .FirstOrDefaultAsync();
        }

        public IEnumerable<Project> LinkedToParatext(string paratextId)
        {
            return MyRepository
                .HasIntegrationSetting("paratext", "ParatextId", paratextId)
                .AsEnumerable();
        }
    }
}
