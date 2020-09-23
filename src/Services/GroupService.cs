using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
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

        public GroupService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            UserRepository userRepository,
            GroupRepository groupRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, groupRepository,  loggerFactory)
        {
            CurrentUserContext = currentUserContext;
            UserRepository = userRepository;
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
    }
}