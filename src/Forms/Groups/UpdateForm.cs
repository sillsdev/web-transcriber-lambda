﻿using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;
using static SIL.Transcriber.Utility.IEnumerableExtensions;

namespace SIL.Transcriber.Forms.Groups
{
    public class UpdateForm : BaseForm
    {
        public UserRepository UserRepository { get; set; }
        public GroupRepository GroupRepository { get; set; }
        public ICurrentUserContext CurrentUserContext { get; }
        public UpdateForm(
            UserRepository userRepository,
            GroupRepository groupRepository,
            ICurrentUserContext currentUserContext): base(userRepository, currentUserContext)
        {
            UserRepository = userRepository;
            GroupRepository = groupRepository;
            CurrentUserContext = currentUserContext;
        }
        public bool IsValid(int id, Group group)
        {
            //If changing owner (which is an organization), validate the change
            CurrentUserOrgIds = CurrentUser.OrganizationIds.OrEmpty();
            var original = GroupRepository.Get()
                                          .Where(g => g.Id == id)
                                          .Include(g => g.Owner)
                                          .FirstOrDefaultAsync().Result;
            if (group.OwnerId != VALUE_NOT_SET)
            {
                if ((!CurrentUserOrgIds.Contains(group.OwnerId)) && (!IsCurrentUserSuperAdmin()))
                {
                    var message = "You do not belong to an organization that the group is owned by and therefor cannot reassign ownership";
                    AddError(message);
                }
            }

            return base.IsValid();
        }
    }
}
