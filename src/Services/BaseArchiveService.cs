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
    public interface IBaseArchiveService<T1, T2> where T1 : BaseModel
    {
    }
    public class BaseArchiveService<TResource> : BaseService<TResource>
         where TResource : BaseModel, IArchive
    {
 
        public BaseArchiveService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<TResource> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
       }

        public override IEnumerable<TResource> GetChanges(int currentuser, string origin, DateTime since, int project)
        {
            IEnumerable<TResource> entities = base.GetChanges(currentuser, origin, since, project);
            return entities.Where(t => !t.Archived);;
        }

        public IEnumerable<TResource> GetDeletedSince(int currentuser, string origin, DateTime since)
        {
            RemoveScopedToCurrentUser(JsonApiContext);                 //avoid the current user thing...
            IEnumerable <TResource> entities = base.GetAsync().Result; //avoid the archived check...
            return base.GetChanges(entities, currentuser, origin, since).Where(t => t.Archived); ;
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
        public override async Task<TResource> UpdateAsync(int id, TResource entity)
        {
            //return unarchived
            TResource existing = await base.GetAsync(id);
            if (existing.Archived)
            {
                throw new Exception("Entity has been deleted. Unable to update.");
            }
            return await base.UpdateAsync(id, entity);
        }
    }
}