using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Forms.Projects;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class ProjectIntegrationService : BaseService<ProjectIntegration>
    {
        public ProjectIntegrationService(
            IJsonApiContext jsonApiContext,
            ProjectIntegrationRepository myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
        }
        public override async Task<IEnumerable<ProjectIntegration>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

    }

}
