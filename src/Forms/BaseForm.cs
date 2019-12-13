using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using Microsoft.EntityFrameworkCore;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Forms
{
    public class BaseForm
    {
        public static int VALUE_NOT_SET = 0;
        public ErrorCollection Errors { get; set; }
        public User CurrentUser { get; }
        protected IEnumerable<int> CurrentUserOrgIds { get; set; }

        public BaseForm(
            UserRepository userRepository,
            ICurrentUserContext currentUserContext
           )
        {
            Errors = new ErrorCollection();
            CurrentUser = userRepository.GetByAuth0Id(currentUserContext.Auth0Id).Result;
        }

        public void AddError(string message, int errorType = 422)
        {
            var error = new Error(errorType, "Validation Failure", message);

            Errors.Add(error);
        }
        protected bool IsValid()
        {
            return Errors.Errors.Count == 0;
        }
        protected bool IsCurrentUserSuperAdmin()
        {
            return CurrentUser.HasOrgRole(RoleName.SuperAdmin, 0);
        }

    }
}
