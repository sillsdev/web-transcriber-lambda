using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ServiceExtensions;

namespace SIL.Transcriber.Services
{
    public class PlanService : BaseArchiveService<Plan>
    {
        public IOrganizationContext OrganizationContext { get; private set; }

        public PlanService(
            IJsonApiContext jsonApiContext,
            IOrganizationContext organizationContext,
            IEntityRepository<Plan> planRepository,
           ILoggerFactory loggerFactory) : base(jsonApiContext, planRepository, loggerFactory)
        {
            OrganizationContext = organizationContext;
        }
        public override async Task<IEnumerable<Plan>> GetAsync()
        {
            return await GetScopedToOrganization<Plan>(
                base.GetAsync,
                OrganizationContext,
                JsonApiContext);

        }

        public override async Task<Plan> GetAsync(int id)
        {
            var plans = await GetAsync();

            return plans.SingleOrDefault(g => g.Id == id);
        }

    }
}