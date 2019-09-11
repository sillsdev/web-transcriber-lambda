using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System.Collections.Generic;
using System.Net.Http;
using SIL.Transcriber.Utility;
using System.Text;
using System;
using SIL.Auth.Models;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class InvitationService : BaseService<Invitation>
    {
        private Auth0ManagementApiTokenService TokenService;
        private HttpClient silAuthClient;
        private OrganizationService OrganizationService;
        public CurrentUserRepository CurrentUserRepository { get; }

        public InvitationService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Invitation> invitationRepository,
            OrganizationService organizationService,
            Auth0ManagementApiTokenService tokenService,
            CurrentUserRepository currentUserRepository,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, invitationRepository, loggerFactory)
        {
            TokenService = tokenService;
            CurrentUserRepository = currentUserRepository;
            OrganizationService = organizationService;
        }
        private HttpClient SILAuthApiClient
        {
            get
            {
                if (silAuthClient == null)
                {
                    silAuthClient = SILIdentity.SILAuthApiClient(TokenService);
                }
                return silAuthClient;
            }
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
            //verify current user is logged in with invitation email
            if (entity.Email !=currentUser.Email)
            {
                throw new System.Exception("Unauthorized.  User must be logged in with invitation email: " + entity.Email);
            }
            var oldentity = MyRepository.GetAsync(id).Result;
            if (entity.Accepted && !oldentity.Accepted)
            {
                //add the user to the org
                var org = await OrganizationService.GetAsync(entity.OrganizationId);
                OrganizationService.JoinOrg(org, currentUser, (RoleName)entity.RoleId, RoleName.Transcriber);
                entity.Accepted = true;
            }
            return await base.UpdateAsync(id, entity);
        }

        public int SendInvitation(Invitation entity)
        {
            User current = CurrentUserRepository.GetCurrentUser().Result;
            if (entity.Organization == null || entity.Organization.SilId == null)
            {
                throw new Exception("Organization does not exist in SIL Identity.");
            }
            var requestObj = new JObject(
                new JProperty("email", entity.Email),
                new JProperty("orgId", entity.Organization.SilId),
                new JProperty("userId", current.SilUserid ?? 1));

            //call the Identity api and receive an invitation id
            HttpResponseMessage response = SILAuthApiClient.PostAsync("invite", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json")).Result;
            if (!response.IsSuccessStatusCode)
                throw new Exception(response.ReasonPhrase);

            var jsonData = SILIdentity.GetData(response.Content.ReadAsStringAsync().Result);
            SILAuth_Invite invite = JsonConvert.DeserializeObject<SILAuth_Invite>(jsonData);

            requestObj = new JObject(
                new JProperty("email", entity.Email),
                new JProperty("orgName", entity.Organization.Name),
                new JProperty("inviteId", invite.id));

            //call the Identity api and receive an invitation id
            SILAuthApiClient.PostAsync("sendEmail", new StringContent(requestObj.ToString(), Encoding.UTF8, "application/json"));

            return invite.id;
        }
    }
}
