using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PassageService : BaseArchiveService<Passage>
    {

        public PassageService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Passage> PassageRepository,
          ILoggerFactory loggerFactory) : base(jsonApiContext, PassageRepository, loggerFactory)
        {
        }

        public override async Task<IEnumerable<Passage>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);

            /*               return await GetScopedToOrganization<Passage>(
                           base.GetAsync,
                           OrganizationContext,
                           JsonApiContext); 

               var entities = await base.GetAsync();
               return ((PassageRepository)MyRepository).UsersPassages(entities); */
        }

        public override async Task<Passage> GetAsync(int id)
        {
            var passages = await GetAsync();

            return passages.SingleOrDefault(g => g.Id == id);
        }
        
    }
}