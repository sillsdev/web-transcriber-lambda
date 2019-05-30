using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Forms.Projects;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class ProjectService : EntityResourceService<Project>
    {
        public IOrganizationContext OrganizationContext { get; private set; }
        public IJsonApiContext JsonApiContext { get; }
        public ICurrentUserContext CurrentUserContext { get; }
        public UserRepository UserRepository { get; }
        public GroupRepository GroupRepository { get; }
        public ProjectRepository ProjectRepository { get; }
        public CurrentUserRepository CurrentUserRepository { get; }
        public IEntityRepository<Organization> OrganizationRepository { get; set; }
        public IEntityRepository<UserRole> UserRolesRepository { get; }

        public ProjectService(
            IJsonApiContext jsonApiContext,
            IOrganizationContext organizationContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            IEntityRepository<Project> projectRepository,
            CurrentUserRepository currentUserRepository,
            GroupRepository groupRepository,
            IEntityRepository<Organization> organizationRepository,
            IEntityRepository<UserRole> userRolesRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, projectRepository, loggerFactory)
        {
            OrganizationContext = organizationContext;
            JsonApiContext = jsonApiContext;
            CurrentUserContext = currentUserContext;
            UserRepository = userRepository;
            GroupRepository = groupRepository;
            OrganizationRepository = organizationRepository;
            UserRolesRepository = userRolesRepository;
            ProjectRepository = (ProjectRepository)projectRepository;
            CurrentUserRepository = currentUserRepository;
        }
        public override async Task<IEnumerable<Project>> GetAsync()
        {
                return await GetScopedToOrganization<Project>(
                    base.GetAsync,
                    OrganizationContext,
                    JsonApiContext);

        }

        public override async Task<Project> GetAsync(int id)
        {
            var projects = await GetAsync();

            return projects.SingleOrDefault(g => g.Id == id);
        }

        public override async Task<Project> UpdateAsync(int id, Project resource)
        {
            //If changing organization, validate the change
            var updateForm = new UpdateForm(UserRepository,
                                           GroupRepository,
                                           CurrentUserContext,
                                           OrganizationRepository,
                                           UserRolesRepository,
                                           OrganizationContext,
                                           ProjectRepository);
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
                                           UserRolesRepository,
                                           OrganizationRepository);
            if (!createForm.IsValid(resource))
            {
                throw new JsonApiException(createForm.Errors);
            }
            var project = await base.CreateAsync(resource);

            return project;
        }
    }

}
