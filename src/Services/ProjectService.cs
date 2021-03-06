﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Forms.Projects;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class ProjectService : BaseArchiveService<Project>
    {
        public ICurrentUserContext CurrentUserContext { get; }
        public UserRepository UserRepository { get; }
        public GroupRepository GroupRepository { get; }
        public OrganizationRepository OrganizationRepository { get; set; }
        private SectionService SectionService;
        private ProjectIntegrationRepository ProjectIntegrationRepository;

        public ProjectService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            ProjectRepository projectRepository,
            GroupRepository groupRepository,
            OrganizationRepository organizationRepository,
            SectionService sectionService,
            ProjectIntegrationRepository projectIntegrationRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, projectRepository, loggerFactory)
        {
            CurrentUserContext = currentUserContext;
            UserRepository = userRepository;
            GroupRepository = groupRepository;
            OrganizationRepository = organizationRepository;
            SectionService = sectionService;
            ProjectIntegrationRepository= projectIntegrationRepository;
    }
    public override async Task<IEnumerable<Project>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<Project> GetAsync(int id)
        {
            var projects = await GetAsync();

            return projects.SingleOrDefault(g => g.Id == id);
        }
        public async Task<Project> GetWithPlansAsync(int id)
        {
            return await ((ProjectRepository)MyRepository).Get()
                              .Where(p => p.Id == id)
                              .Include(p => p.Plans).FirstOrDefaultAsync();
        }
        public override async Task<Project> UpdateAsync(int id, Project resource)
        {
            //If changing organization, validate the change
            var updateForm = new UpdateForm(UserRepository,
                                           GroupRepository,
                                           CurrentUserContext,
                                           OrganizationRepository,
                                           (ProjectRepository)MyRepository);
            if (!updateForm.IsValid(id, resource))
            {
                throw new JsonApiException(updateForm.Errors);
            }

            var project = await base.UpdateAsync(id, resource);
            return project;
        }

        public override async Task<Project> CreateAsync(Project resource)
        {
            var createForm = new CreateForm(UserRepository,
                                           GroupRepository,
                                           CurrentUserContext,
                                           OrganizationRepository);
            if (!createForm.IsValid(resource))
            {
                throw new JsonApiException(createForm.Errors);
            }
            var project = await base.CreateAsync(resource);

            return project;
        }
        public string ParatextProject(int projectId)
        {
            var paratextSettings = IntegrationSettings(projectId, "paratext");
            if ((paratextSettings ?? "") == "")
            {
                return "";
            }
            dynamic settings = JsonConvert.DeserializeObject(paratextSettings);
            return settings.ParatextId;
        }
        public string IntegrationSettings(int projectId, string integration)
        {
            return ProjectIntegrationRepository.IntegrationSettings(projectId, integration);
        }
        public IEnumerable<Section> GetSectionsAtStatus(int projectId, string status)
        {
            return SectionService.GetSectionsAtStatus(projectId, status);
        }
        public bool IsLinkedToParatext(Project p, string ParatextId)
        {
            return ParatextProject(p.Id) == ParatextId;
        }
        public IEnumerable<Project> LinkedToParatext(string paratextId)
        {
            ProjectRepository pr = (ProjectRepository)MyRepository;
            return pr.HasIntegrationSetting("paratext", "ParatextId", paratextId).AsEnumerable<Project>(); 
        }
    }

}
