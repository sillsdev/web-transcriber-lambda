using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class BaseService<TResource> : EntityResourceService<TResource>
        where TResource : BaseModel
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
        public IEnumerable<TResource> GetChangedSince(DateTime since)
        {
            return GetAsync().Result.Where(h => h.DateUpdated > since);
        }

    }
}

