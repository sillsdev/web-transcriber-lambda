﻿using System;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Forms.GroupMemberships;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class GroupMembershipService : BaseService<GroupMembership>
    {
        public GroupMembershipService(
            IJsonApiContext jsonApiContext,
            UserRepository userRepository,
            ProjectRepository projectRepository,
            ICurrentUserContext currentUserContext,
            IEntityRepository<GroupMembership> groupMembershipRepository,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, groupMembershipRepository, loggerFactory)
        {
            UserRepository = userRepository;
            ProjectRepository = projectRepository;
            CurrentUserContext = currentUserContext;
            GroupMembershipRepository = groupMembershipRepository;
        }

        public UserRepository UserRepository { get; }
        public ProjectRepository ProjectRepository { get; }
        public ICurrentUserContext CurrentUserContext { get; }
        public IEntityRepository<GroupMembership> GroupMembershipRepository { get; }

        public override async Task<bool> DeleteAsync(int id)
        {
            var deleteForm = new DeleteForm(UserRepository,
                                            ProjectRepository,
                                            GroupMembershipRepository,
                                            CurrentUserContext);
            if (!deleteForm.IsValid(id))
            {
                throw new JsonApiException(deleteForm.Errors);
            }

            return await base.DeleteAsync(id);
        }
    }
}
