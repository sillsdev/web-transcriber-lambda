
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class ProjecttypeService : BaseService<ProjectType>
    {

        public ProjecttypeService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<ProjectType> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
        }
    }
}
