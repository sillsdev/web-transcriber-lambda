using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.ServiceExtensions;

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
        public virtual IEnumerable<TResource> GetChanges(int currentuser, string origin, DateTime since, int project)
        {
            if (currentuser > 0)
                return GetChanges(GetAsync().Result, currentuser, origin, since);
            else
                return GetChanges(GetByProjAsync(project).Result, currentuser, origin, since);
        }
        public async Task<IEnumerable<TResource>> GetByProjAsync(int project)
        {
            return await GetScopedToProjects(
                base.GetAsync,
                JsonApiContext, 
                project);
        }

        public IEnumerable<TResource> GetChanges(IEnumerable<TResource> entities, int currentuser, string origin, DateTime since)
        {
            if (entities == null) return null;
            if (currentuser > 0)
                return entities.Where(p => (p.LastModifiedBy != currentuser || p.LastModifiedOrigin != origin) && p.DateUpdated > since);
            return entities.Where(p => p.LastModifiedOrigin != origin && p.DateUpdated > since);
        }
    }
}

