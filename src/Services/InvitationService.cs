using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using static SIL.Transcriber.Utility.ResourceHelpers;

namespace SIL.Transcriber.Services
{
    public class InvitationService : BaseService<Invitation>
    {
        readonly private OrganizationRepository OrganizationRepository;
        readonly private OrganizationService OrganizationService;
        readonly private GroupMembershipService GroupMembershipService;
        readonly private UserRepository UserRepository;
        public CurrentUserRepository CurrentUserRepository { get; }

        public InvitationService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Invitation> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            GroupMembershipService groupMembershipService,
            CurrentUserRepository currentUserRepository,
            OrganizationService organizationService,
            OrganizationRepository organizationRepository,
            UserRepository userRepository,
            InvitationRepository repository
        )
            : base(
                repositoryAccessor,
                queryLayerComposer,
                paginationContext,
                options,
                loggerFactory,
                request,
                resourceChangeTracker,
                resourceDefinitionAccessor,
                repository
            )
        {
            CurrentUserRepository = currentUserRepository;
            OrganizationService = organizationService;
            OrganizationRepository = organizationRepository;
            UserRepository = userRepository;
            GroupMembershipService = groupMembershipService;
        }

        private static string BuildEmailBody(dynamic strings, Invitation entity)
        {
            //localize...
            string app = strings["App"] ?? "missing App: SIL Transcriber";
            string invite = strings["Invitation"] ?? "missing Invitation: has invited you to join";
            string instructions =
                strings["Instructions"]
                ?? "missing Instructions: Please click the following link to accept the invitation.";
            string SILorg = strings["SILOrg"] ?? "missing SILOrg: SIL International";
            string questions = strings["Questions"] ?? "missing Questions: Questions? Contact ";
            string join = strings["Join"] ?? "missing Join: Join ";

            string link = entity.LoginLink + "?inviteId=" + entity.Id;

            string html = LoadResource("invitation.html");

            return html.Replace("{{App}}", app)
                .Replace(
                    "{Invitation}",
                    string.Format(
                        "{0} {1} '{2}'.",
                        entity.InvitedBy,
                        invite,
                        entity.Organization.Name
                    )
                )
                .Replace("{{Instructions}}", instructions)
                .Replace("{Button}", join + ' ' + entity.Organization.Name) //TODO
                .Replace("{Link}", link)
                .Replace("{Questions}", questions + ' ' + entity.InvitedBy)
                .Replace("{Year}", DateTime.Today.Year.ToString())
                .Replace("{{SIL}}", SILorg);
        }

        public override async Task<Invitation?> CreateAsync(
            Invitation entity,
            CancellationToken cancellationToken
        )
        {
            if (entity.Organization == null)
                throw new Exception("Organization must be set");
            try
            {
                dynamic strings = JObject.Parse(entity.Strings);

                Invitation? dbentity = await base.CreateAsync(entity, cancellationToken);
                if (dbentity == null)
                    return null;

                string subject =
                    strings["Subject"] ?? "missing subject: SIL Transcriber Invitation";
                await TranscriberAPI.Utility.Email.SendEmailAsync(
                    entity.Email,
                    subject,
                    BuildEmailBody(strings, dbentity)
                );
                return dbentity;
            }
            catch (Exception ex)
            {
                Console.WriteLine("The email was not sent so invitation was deleted.");
                Console.WriteLine("Error message: " + ex.Message);
                throw new Exception(
                    "The email was not sent so invitation was deleted.\n" + ex.Message
                );
            }
        }

        public override async Task<Invitation?> UpdateAsync(
            int id,
            Invitation entity,
            CancellationToken cancellationToken
        )
        {
            User? currentUser = CurrentUserRepository.GetCurrentUser();
            Invitation? oldentity = GetAsync(id, cancellationToken).Result;
            if (oldentity == null)
                return null;

            //verify current user is logged in with invitation email
            if (
                (entity.Email ?? oldentity.Email ?? "").ToLower()
                != (currentUser?.Email ?? "").ToLower()
            )
            {
                throw new System.Exception(
                    "Unauthorized.  User must be logged in with invitation email: "
                        + oldentity?.Email
                        + "  Currently logged in as "
                        + currentUser?.Email
                );
            }
            Organization? org;
            if (
                currentUser != null
                && entity.Accepted
                && !oldentity.Accepted
                && (
                    org = OrganizationRepository
                        .Get()
                        .Where(o => o.Id == oldentity.OrganizationId)
                        .FirstOrDefault()
                ) != null
            )
            {
                //add the user to the org
                OrganizationService.JoinOrg(
                    org,
                    currentUser,
                    (RoleName)oldentity.RoleId,
                    (RoleName)oldentity.AllUsersRoleId
                );
                if (entity.GroupId != null)
                {
                    if (entity.GroupRoleId == null)
                        entity.GroupRoleId = (int)RoleName.Transcriber;
                    _ = GroupMembershipService.JoinGroup(
                        currentUser.Id,
                        (int)entity.GroupId,
                        (RoleName)entity.GroupRoleId
                    );
                }
                //update the user so all other users in the new org get the user downloaded with the next datachange
                UserRepository.Refresh(currentUser);
            }
            //updateAsync now returns null if no changes were actually made :/
            return await base.UpdateAsync(id, entity, cancellationToken) ?? entity;
        }
    }
}
