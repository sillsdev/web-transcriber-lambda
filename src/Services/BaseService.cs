using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using System.Data.Common;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class BaseService<TResource> : EntityResourceService<TResource>
        where TResource : class, IIdentifiable<int>
    {
        protected IEntityRepository<TResource> MyRepository { get; }
        protected IJsonApiContext JsonApiContext { get; }
        protected ILogger<TResource> Logger { get; set; }


        public BaseService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<TResource>myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
            this.MyRepository = myRepository;
            JsonApiContext = jsonApiContext;
            this.Logger = loggerFactory.CreateLogger<TResource>();
        }

    }
}

