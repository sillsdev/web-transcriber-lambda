using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System.Data.Common;
using System.Threading.Tasks;

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
        //also a nice try but jsonapi filter eats it.
        public override async Task<bool> DeleteAsync(int id)
        {
            try
            {
                return await base.DeleteAsync(id);
            }
            catch (DbException ex)
            {
                throw ex;
            }
        }
    }
}

