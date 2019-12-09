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
         where TResource : BaseModel, IIdentifiable<int>, IArchive
    {
        public BaseArchiveService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<TResource> myRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, myRepository, loggerFactory)
        {
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