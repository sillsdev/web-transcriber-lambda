﻿using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;


namespace SIL.Transcriber.Repositories
{ 
    public class InvitationRepository : BaseRepository<Invitation>
    {
        public InvitationRepository(
          ILoggerFactory loggerFactory,
          IJsonApiContext jsonApiContext,
          CurrentUserRepository currentUserRepository,
          //EntityHooksService<Project> statusUpdateService,
          IDbContextResolver contextResolver
      ) : base(loggerFactory, jsonApiContext, currentUserRepository, contextResolver)
        {
        }
    }
}
