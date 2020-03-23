using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Serialization;
using SIL.Transcriber.Data;
using SIL.Transcriber.Repositories;


namespace SIL.Transcriber.Services
{
    public class OrgDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly IJsonApiDeSerializer jsonApiDeSerializer;
        protected readonly OrganizationService organizationService;
        protected readonly GroupMembershipService gmService;
        protected readonly CurrentUserRepository currentUserRepository;

        public OrgDataService(IDbContextResolver contextResolver, IJsonApiSerializer jsonSer, IJsonApiDeSerializer jsonDeser, CurrentUserRepository currentUserRepo,
            OrganizationService orgService,
            GroupMembershipService grpMemService)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            jsonApiDeSerializer = jsonDeser;
            currentUserRepository = currentUserRepo;
            organizationService = orgService;
            gmService = grpMemService;
        }

    }
}
