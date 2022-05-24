using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Newtonsoft.Json;

namespace SIL.Transcriber.Services
{
    public class ProjectService : BaseArchiveService<Project>
    {
        private readonly ProjectIntegrationRepository ProjectIntegrationRepository;
        ProjectRepository MyRepository;
        public ProjectService(
            IResourceRepositoryAccessor repositoryAccessor, IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext, IJsonApiOptions options, ILoggerFactory loggerFactory,
            IJsonApiRequest request, IResourceChangeTracker<Project> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            ProjectIntegrationRepository projectIntegrationRepository, ProjectRepository myRepository) 
            : base(repositoryAccessor, queryLayerComposer, paginationContext, options, loggerFactory, request,
                resourceChangeTracker, resourceDefinitionAccessor, myRepository)
        {
            ProjectIntegrationRepository= projectIntegrationRepository;
            MyRepository = myRepository;
    }
        public async Task<Project?> GetWithPlansAsync(int id)
        {
            return await ((ProjectRepository)MyRepository).Get()
                              .Where(p => p.Id == id)
                              .Include(p => p.Plans).FirstOrDefaultAsync();
        }
        public string ParatextProject(int projectId, string type)
        {
            string? paratextSettings = IntegrationSettings(projectId, "paratext"+type);
            if (paratextSettings == null || paratextSettings == "")
            {
                return "";
            }
            dynamic? settings = JsonConvert.DeserializeObject(paratextSettings);
            return settings?.ParatextId??"";
        }
        public string IntegrationSettings(int projectId, string integration)
        {
            return ProjectIntegrationRepository.IntegrationSettings(projectId, integration);
        }

        public bool IsLinkedToParatext(Project p, string artifactType,  string ParatextId)
        {
            return ParatextProject(p.Id, artifactType) == ParatextId;
        }
        public IEnumerable<Project> LinkedToParatext(string paratextId)
        {
            return MyRepository.HasIntegrationSetting("paratext", "ParatextId", paratextId).AsEnumerable<Project>(); 
        }
    }

}
