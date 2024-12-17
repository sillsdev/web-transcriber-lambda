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
    public class ProjectService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Project> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        ProjectRepository myRepository
        ) : BaseArchiveService<Project>(
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
        private readonly ProjectRepository MyRepository = myRepository;

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
