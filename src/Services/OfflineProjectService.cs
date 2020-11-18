using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.ServiceExtensions;
using static SIL.Transcriber.Utility.ResourceHelpers;

namespace SIL.Transcriber.Services
{
    public class OfflineProjectService : BaseService<OfflineProject>
    {
        private ILoggerFactory LoggerFactory;
        private OrganizationService OrganizationService;
        private GroupMembershipRepository GroupMembershipRepository;
        public CurrentUserRepository CurrentUserRepository { get; }

        public OfflineProjectService(
            IJsonApiContext jsonApiContext,
            OfflineProjectRepository OfflineProjectRepository,
            CurrentUserRepository currentUserRepository,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, OfflineProjectRepository, loggerFactory)
        {
            LoggerFactory = loggerFactory;
            CurrentUserRepository = currentUserRepository;
        }
        public override async Task<IEnumerable<OfflineProject>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);
        }
    }
}
