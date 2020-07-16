using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Utility;
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
            IEntityRepository<TResource> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
            this.MyRepository = myRepository;
            JsonApiContext = jsonApiContext;
            this.Logger = loggerFactory.CreateLogger<TResource>();
        }
        public virtual IEnumerable<TResource> GetChanges(int currentuser, string origin, DateTime since)
        {
            return GetChanges(GetAsync().Result, currentuser, origin, since);
        }
        public IEnumerable<TResource> GetChanges(IEnumerable<TResource> entities, int currentuser, string origin, DateTime since)
        {
            if (entities == null) return null;
            return entities.Where(p => (p.LastModifiedBy != currentuser || p.LastModifiedOrigin != origin) && p.DateUpdated > since);
        }
    }
}

