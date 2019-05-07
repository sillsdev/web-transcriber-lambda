using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class UserPassageService : EntityResourceService<UserPassage>
    {
        public IOrganizationContext OrganizationContext { get; }
        public IEntityRepository<UserPassage> UserPassageRepository { get; }
        public CurrentUserRepository CurrentUserRepository { get; }
        public IJsonApiContext JsonApiContext { get; }

        public UserPassageService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<UserPassage> userPassageRepository,
            CurrentUserRepository currentUserRepository,
            IOrganizationContext organizationContext,
            ILoggerFactory loggerFactory) 
            : base(jsonApiContext, userPassageRepository, loggerFactory)
        {
            this.OrganizationContext = organizationContext;
            this.UserPassageRepository = userPassageRepository;
            this.CurrentUserRepository = currentUserRepository;
            this.JsonApiContext = jsonApiContext;
        }

        public override async Task<IEnumerable<UserPassage>> GetAsync()
        {
            if (this.OrganizationContext.IsOrganizationHeaderPresent) 
            {
                return await GetScopedToOrganization<UserPassage>(
                    base.GetAsync,
                    this.OrganizationContext,
                    JsonApiContext);
            }

            return await base.GetAsync();
        }
    }
}
