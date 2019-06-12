
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class PlantypeService : BaseService<PlanType>
    {

        public PlantypeService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<PlanType> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
        }
    }
}
