using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class WorkflowStepService : BaseArchiveService<WorkflowStep>
    {
        public WorkflowStepService(
            IJsonApiContext jsonApiContext,
            WorkflowStepRepository WorkflowStepRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, WorkflowStepRepository, loggerFactory)
        {
        }
    }
}
