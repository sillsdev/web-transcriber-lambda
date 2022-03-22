using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class OrgWorkflowStepService : BaseArchiveService<OrgWorkflowStep>
    {
        public OrgWorkflowStepService(
            IJsonApiContext jsonApiContext,
            OrgWorkflowStepRepository OrgWorkflowStepRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, OrgWorkflowStepRepository, loggerFactory)
        {
        }
        public override async Task<IEnumerable<OrgWorkflowStep>> GetAsync()
        {
            return await GetScopedToCurrentUser(
              base.GetAsync,
              JsonApiContext);
        }

        public override async Task<OrgWorkflowStep> GetAsync(int id)
        {
            IEnumerable<OrgWorkflowStep> WorkflowSteps = await GetAsync();

            return WorkflowSteps.SingleOrDefault(g => g.Id == id);
        }
    }
}
