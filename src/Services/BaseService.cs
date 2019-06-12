using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;


namespace SIL.Transcriber.Services
{
    public class BaseService<TResource> : EntityResourceService<TResource>
        where TResource : class, IIdentifiable<int>
    {
        public IEntityRepository<TResource> MyRepository { get; }
        public IJsonApiContext JsonApiContext { get; }
 
        public BaseService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<TResource>myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
            this.MyRepository = myRepository;
            JsonApiContext = jsonApiContext;
        } 
    }
}

