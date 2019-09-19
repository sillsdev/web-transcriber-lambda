using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Threading.Tasks;


namespace SIL.Transcriber.Services
{
    public class InvitationService : BaseService<Invitation>
    {
        //private Auth0ManagementApiTokenService TokenService;
        private OrganizationService OrganizationService;
        public CurrentUserRepository CurrentUserRepository { get; }
        private ISILIdentityService SILIdentity;

        public InvitationService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Invitation> invitationRepository,
            OrganizationService organizationService,
            CurrentUserRepository currentUserRepository,
            ISILIdentityService silIdentityService,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, invitationRepository, loggerFactory)
        {
            //TokenService = tokenService;
            CurrentUserRepository = currentUserRepository;
            OrganizationService = organizationService;
            SILIdentity = silIdentityService;
        }
        public override async Task<Invitation> CreateAsync(Invitation entity)
        {
            //call the Identity api and receive an invitation id
            var org = await OrganizationService.GetAsync(entity.OrganizationId);
            entity.Organization = org;
            entity.SilId = SendInvitation(entity);
            return await base.CreateAsync(entity);
        }

        public override async Task<Invitation> UpdateAsync(int id, Invitation entity)
        {
            var currentUser = CurrentUserRepository.GetCurrentUser().Result;
            var oldentity = MyRepository.GetAsync(id).Result;
            //verify current user is logged in with invitation email
            if (oldentity.Email !=currentUser.Email)
            {
                throw new System.Exception("Unauthorized.  User must be logged in with invitation email: " + oldentity.Email + "  Currently logged in as " + currentUser.Email);
            }
            if (entity.Accepted && !oldentity.Accepted)
            {
                //add the user to the org
                var org = await OrganizationService.GetAsync(oldentity.OrganizationId);
                OrganizationService.JoinOrg(org, currentUser, (RoleName)oldentity.RoleId, RoleName.Transcriber);
            }
            return await base.UpdateAsync(id, entity);
        }

        public int SendInvitation(Invitation entity)
        {
            User current = CurrentUserRepository.GetCurrentUser().Result;
            if (entity.Organization == null || entity.Organization.SilId == 0)
            {
                throw new Exception("Organization does not exist in SIL Identity.");
            }
            return SILIdentity.CreateInvite(entity.Email, entity.Organization.Name, entity.Organization.SilId,current.SilUserid ?? 1);
        }
    }
}
