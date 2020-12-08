using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Forms.Groups;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class GroupService : BaseArchiveService<Group>
    {
        public ICurrentUserContext CurrentUserContext { get; }
        public UserRepository UserRepository { get; }
        public ProjectRepository ProjectRepository { get; }

        public GroupService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            GroupRepository groupRepository,
            ProjectRepository projectRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, groupRepository,  loggerFactory)
        {
            CurrentUserContext = currentUserContext;
            UserRepository = userRepository;
            ProjectRepository = projectRepository;
        }


        public override async Task<IEnumerable<Group>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }
        public override async Task<Group> GetAsync(int id)
        {
            var groups = await GetAsync();
            return groups.SingleOrDefault(g => g.Id == id);
        }
        public override async Task<Group> UpdateAsync(int id, Group resource)
        {
            var updateForm = new UpdateForm(UserRepository,
                                             (GroupRepository)MyRepository,
                                            CurrentUserContext);
            if (!updateForm.IsValid(id, resource))
            {
                throw new JsonApiException(updateForm.Errors);
            }
            return await base.UpdateAsync(id, resource);
        }

        public override async Task<bool> DeleteAsync(int id)
        {
            var deleteForm = new DeleteForm(UserRepository,
                                            ProjectRepository,
                                             (GroupRepository)MyRepository,
                                            CurrentUserContext);
            if (!deleteForm.IsValid(id))
            {
                throw new JsonApiException(deleteForm.Errors);
            }

            return await base.DeleteAsync(id);
        }
    }
}