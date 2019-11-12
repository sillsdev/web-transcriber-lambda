using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Threading.Tasks;
using TranscriberAPI.Utility;

namespace SIL.Transcriber.Services
{
    public class InvitationService : BaseService<Invitation>
    {
        //private Auth0ManagementApiTokenService TokenService;
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
            //TokenService = tokenService;
            CurrentUserRepository = currentUserRepository;
            OrganizationService = organizationService;
            GroupMembershipRepository = groupMembershipRepository;
            //SILIdentity = silIdentityService;
        }

        private string BuildEmailBody(dynamic strings, Invitation entity)
        {
            //localize...
            string app = strings["App"] ?? "missing App: SIL Transcriber";
            string invite = strings["Invitation"] ?? "missing Invitation: You have been invited to join";
            string instructions = strings["Instructions"] ?? "missing Instructions: Please click the following link to accept the invitation.";
            string SILorg = strings["SILOrg"] ?? "missing SILOrg: SIL International";

            string link = entity.LoginLink + "?inviteId=" + entity.Id;
            string href = string.Format("<a href=\"{0}\" target=\"_blank\" rel=\"noopener\">{0}</a>", link);

            const string header = "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\"><html xmlns=\"http://www.w3.org/1999/xhtml\"><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\" /><title>Demystifying Email Design</title><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"/></head><body style=\"margin: 0; padding: 0;\">";
            const string table1 = "<table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td style=\"padding: 10px 0 30px 0;\"><table align=\"center\" border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\" style=\"border: 1px solid #cccccc; border-collapse: collapse;\"><tbody><tr><td align=\"center\" bgcolor=\"#70BBD9\"><img src=\"https://sil-transcriber-logos.s3.amazonaws.com/transcriber9.png\" alt=\"\" width=\"200\" height=\"200\" /></td><td bgcolor = \"#70BBD9\" style = \"padding: 40px 0px 30px; color: #153643; font-size: 28px; font-weight: bold; font-family: Arial, sans-serif; width: 294px;\" >";
            const string table2 = "</td></tr><tr><td bgcolor=\"#FFFFFF\" style=\"padding: 40px 30px 40px 30px;\" colspan=\"2\"><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td style=\"color: #153643; font-family: Arial, sans-serif; font-size: 24px;\"><b>";
            const string table3 = "</b></td></tr><tr><td style=\"padding: 20px 0 30px 0; color: #153643; font-family: Arial, sans-serif; font-size: 16px; line-height: 20px;\">";
            const string table4 = "<br /> <br />";
            const string table5 = "</td></tr><tr><td><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"260\" valign=\"top\"><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td><img src=\"https://s3-us-west-2.amazonaws.com/s.cdpn.io/210284/left.gif\" alt=\"\" width=\"100%\" height=\"140\" style=\"display: block;\" /></td></tr><tr><td style=\"padding: 25px 0 0 0; color: #153643; font-family: Arial, sans-serif; font-size: 16px; line-height: 20px;\"></td></tr></tbody></table></td><td style=\"font-size: 0; line-height: 0;\" width=\"20\">&nbsp;</td><td width=\"260\" valign=\"top\"><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td><img src=\"https://s3-us-west-2.amazonaws.com/s.cdpn.io/210284/right.gif\" alt=\"\" width=\"100%\" height=\"140\" style=\"display: block;\" /></td></tr><tr><td style=\"padding: 25px 0 0 0; color: #153643; font-family: Arial, sans-serif; font-size: 16px; line-height: 20px;\"></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr><tr><td bgcolor=\"#EE4C50\" style=\"padding: 30px; width: 305px;\" colspan=\"2\"><table border = \"0\" cellpadding = \"0\" cellspacing = \"0\" width = \"100%\"><tbody><tr><td style = \"color: #ffffff; font-family: Arial, sans-serif; font-size: 14px;\" width = \"75%\"> &reg; <a href = \"https://www.sil.org/\">";
            const string table6 = "</a> 2019</td><td align=\"right\" width=\"25%\"><table border=\"0\" cellpadding=\"0\" cellspacing=\"0\"><tbody><tr><td style=\"font-family: Arial, sans-serif; font-size: 12px; font-weight: bold;\"></td><td style=\"font-size: 0; line-height: 0;\" width=\"20\">&nbsp;</td><td style=\"font-family: Arial, sans-serif; font-size: 12px; font-weight: bold;\"></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></body></html>";

            return string.Format("{0}{1}{2}{3}{4} '{5}'. {6}{7}{8}{9}{10}{11}{12}", header, table1, app, table2, invite, entity.Organization.Name, table3, instructions, table4, href, table5, SILorg, table6);
        }

        public override async Task<Invitation> CreateAsync(Invitation entity)
        {
            //call the Identity api and receive an invitation id
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
                Email.SendEmail(entity.Email, subject, BuildEmailBody(strings, entity));
                return entity;
            }
            catch (Exception ex)
            {
                base.DeleteAsync(entity.Id); //yes I know, I'm not going to wait
                Console.WriteLine("The email was not sent.");
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
