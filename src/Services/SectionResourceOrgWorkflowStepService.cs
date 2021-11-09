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
    public class SectionResourceOrgWorkflowStepService : BaseArchiveService<SectionResourceOrgWorkflowStep>
    {
        public SectionResourceOrgWorkflowStepService(
            IJsonApiContext jsonApiContext,
           SectionResourceOrgWorkflowStepRepository SectionResourceOrgWorkflowStepRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, SectionResourceOrgWorkflowStepRepository, loggerFactory)
        {
        }

    }
}