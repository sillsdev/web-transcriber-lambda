
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Services
{
        public class ActivitystateService : BaseService<Activitystate>
        {

            public ActivitystateService(
                IJsonApiContext jsonApiContext,
                AppDbContextRepository<Activitystate> myRepository,
                ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
            {
            }   
        }
}
