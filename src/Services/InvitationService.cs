using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TranscriberAPI.Utility;

namespace SIL.Transcriber.Services
{
    public class InvitationService : BaseService<Invitation>
    {
        private ILoggerFactory LoggerFactory;
        private OrganizationService OrganizationService;
        private GroupMembershipRepository GroupMembershipRepository;
        public CurrentUserRepository CurrentUserRepository { get; }
        //private ISILIdentityService SILIdentity;

        public InvitationService(
            IJsonApiContext jsonApiContext,
            IEntityRepository<Invitation> invitationRepository,
            OrganizationService organizationService,
            GroupMembershipRepository groupMembershipRepository,
            CurrentUserRepository currentUserRepository,
            //ISILIdentityService silIdentityService,
            ILoggerFactory loggerFactory
        ) : base(jsonApiContext, invitationRepository, loggerFactory)
        {
            LoggerFactory = loggerFactory;
            CurrentUserRepository = currentUserRepository;
            OrganizationService = organizationService;
            GroupMembershipRepository = groupMembershipRepository;
            //SILIdentity = silIdentityService;
        }
        private string LoadResource(string name) {
            //Load the file
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith(name));
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
               return  reader.ReadToEnd();
            }
        }
        private string BuildEmailBody(dynamic strings, Invitation entity)
        {
            //localize...
            string app = strings["App"] ?? "missing App: SIL Transcriber";
            string invite = strings["Invitation"] ?? "missing Invitation: has invited you to join";
            string instructions = strings["Instructions"] ?? "missing Instructions: Please click the following link to accept the invitation.";
            string SILorg = strings["SILOrg"] ?? "missing SILOrg: SIL International";
            string questions = strings["Questions"] ?? "missing Questions: Questions? Contact ";
            string join = strings["Join"] ?? "missing Join: Join ";

            string link = entity.LoginLink + "?inviteId=" + entity.Id;

            string html = LoadResource("invitation.html");
            
            return html.Replace("{{App}}", app)
                        .Replace("{Invitation}", string.Format("{0} {1} '{2}'.", entity.InvitedBy, invite, entity.Organization.Name))
                        .Replace("{{Instructions}}", instructions)
                        .Replace("{Button}", join + ' ' + entity.Organization.Name) //TODO
                        .Replace("{Link}", link)
                        .Replace("{Questions}", questions + ' ' + entity.InvitedBy)
                        .Replace("{Year}", DateTime.Today.Year.ToString())
                        .Replace("{{SIL}}", SILorg);
        }

        public override async Task<Invitation> CreateAsync(Invitation entity)
        {
            //call the Identity api and receive an invitation id
            Console.WriteLine("creating invitation");
            var org = await OrganizationService.GetAsync(entity.OrganizationId);
            entity.Organization = org;
            //entity.SilId = SendInvitation(entity);
            entity = await base.CreateAsync(entity);
            entity.SilId = entity.Id;
            await base.UpdateAsync(entity.Id, entity);
            try
            {
                dynamic strings = JObject.Parse(entity.Strings);
                string subject = strings["Subject"] ?? "missing subject: SIL Transcriber Invitation";
                await Email.SendEmailAsync(entity.Email, subject, BuildEmailBody(strings, entity));
                return entity;
            }
            catch (Exception ex)
            {
                await base.DeleteAsync(entity.Id); 
                Console.WriteLine("The email was not sent so invitation was deleted.");
                Console.WriteLine("Error message: " + ex.Message);
                throw ex;
            }
        }

        public override async Task<Invitation> UpdateAsync(int id, Invitation entity)
        {
            var currentUser = CurrentUserRepository.GetCurrentUser().Result;
            var oldentity = MyRepository.GetAsync(id).Result;
            //verify current user is logged in with invitation email
            if (oldentity.Email != currentUser.Email)
            {
                throw new System.Exception("Unauthorized.  User must be logged in with invitation email: " + oldentity.Email + "  Currently logged in as " + currentUser.Email);
            }
            if (entity.Accepted && !oldentity.Accepted)
            {
                //add the user to the org
                var org = await OrganizationService.GetAsync(oldentity.OrganizationId);
                OrganizationService.JoinOrg(org, currentUser, (RoleName)oldentity.RoleId, (RoleName)oldentity.AllUsersRoleId);
                if (oldentity.GroupId != null)
                {
                    if (oldentity.GroupRoleId == null)
                        oldentity.GroupRoleId = (int)RoleName.Transcriber;
                    GroupMembershipRepository.JoinGroup(currentUser.Id, (int)oldentity.GroupId, (RoleName)oldentity.GroupRoleId);
                }
            }
            return await base.UpdateAsync(id, entity);
        }
        /*
        public int SendInvitation(Invitation entity)
        {
            User current = CurrentUserRepository.GetCurrentUser().Result;
            if (entity.Organization == null || entity.Organization.SilId == 0)
            {
                throw new Exception("Organization does not exist in SIL Identity.");
            }
            return SILIdentity.CreateInvite(entity.Email, entity.Organization.Name, entity.Organization.SilId,current.SilUserid ?? 1);
        }
        */
    }
}
