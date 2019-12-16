using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class BaseArchiveService<TResource> : BaseService<TResource>
         where TResource : BaseModel, IArchive
    {
        public BaseArchiveService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<TResource> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
        }
        public async Task<IEnumerable<TResource>> GetDeleted()
        {
            //return archived
            IEnumerable<TResource> entities = await base.GetAsync();
            if (typeof(IArchive).IsAssignableFrom(typeof(TResource)))
            {
                entities = entities.Where(t => t.Archived);
            }
            return entities;
        }
        public new IEnumerable<TResource> GetChanges(IEnumerable<TResource> entities, int currentuser, string origin, DateTime since)
        {
            return entities.Where(p => (p.LastModifiedBy != currentuser || p.LastModifiedOrigin != origin) && p.DateUpdated > since);
        }
        public IEnumerable<TResource> GetDeletedSince(int currentuser, string origin, DateTime since)
        {
            return GetChanges(GetDeleted().Result, currentuser, origin, since);
        }
        public override async Task<IEnumerable<TResource>> GetAsync()
        {
            //return unarchived
            IEnumerable<TResource> entities = await base.GetAsync();
            if (typeof(IArchive).IsAssignableFrom(typeof(TResource)))
            {
                entities = entities.Where(t => !t.Archived);
            }
            return entities;
        }
    }
}