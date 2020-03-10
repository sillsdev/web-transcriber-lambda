using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PlanService : BaseArchiveService<Plan>
    {

        public PlanService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Plan> planRepository,
           ILoggerFactory loggerFactory) : base(jsonApiContext, planRepository, loggerFactory)
        {
        }
        public override async Task<IEnumerable<Plan>> GetAsync()
        {
            return await GetScopedToCurrentUser(
                base.GetAsync,
                JsonApiContext);


        }
        public Plan Get(int id)
        {
            return MyRepository.Get().Where(p => p.Id == id).FirstOrDefault();
        }
        public Plan GetWithSections(int id)
        {
            return MyRepository.Get().Where(p => p.Id == id).Include(p => p.Sections).ThenInclude(s=> s.Passages).FirstOrDefault();
        }
        public override async Task<Plan> GetAsync(int id)
        {
            var plans = await GetAsync();

            return plans.SingleOrDefault(g => g.Id == id);
        }

    }
}