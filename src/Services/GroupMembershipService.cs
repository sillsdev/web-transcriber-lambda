﻿using System;
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
            IEntityRepository<UserRole> userRolesRepository,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, groupMembershipRepository, loggerFactory)
        {
            UserRepository = userRepository;
            ProjectRepository = projectRepository;
            CurrentUserContext = currentUserContext;
            GroupMembershipRepository = groupMembershipRepository;
            UserRolesRepository = userRolesRepository;
        }

        public UserRepository UserRepository { get; }
        public ProjectRepository ProjectRepository { get; }
        public ICurrentUserContext CurrentUserContext { get; }
        public IEntityRepository<GroupMembership> GroupMembershipRepository { get; }
        public IEntityRepository<UserRole> UserRolesRepository { get; }

        public override async Task<bool> DeleteAsync(int id)
        {
            var deleteForm = new DeleteForm(UserRepository,
                                            ProjectRepository,
                                            GroupMembershipRepository,
                                            UserRolesRepository,
                                            CurrentUserContext);
            if (!deleteForm.IsValid(id))
            {
                throw new JsonApiException(deleteForm.Errors);
            }

            return await base.DeleteAsync(id);
        }
    }
}
