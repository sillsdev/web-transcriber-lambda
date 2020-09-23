using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SIL.Transcriber.Services
{
    public class DataChangeService : BaseService<DataChanges>
    {
        public ICurrentUserContext CurrentUserContext { get; }
        GroupMembershipService GMService;
        GroupService GroupService;
        InvitationService InvitationService;
        MediafileService MediafileService;
        OrganizationMembershipService OrgMemService;
        OrganizationService OrganizationService;
        PassageService PassageService;
        PassageStateChangeService PassageStateChangeService;
        PlanService PlanService;
        ProjectIntegrationService ProjIntService;
        ProjectService ProjectService;
        SectionService SectionService;
        UserService UserService;
        CurrentUserRepository CurrentUserRepository;

        public DataChangeService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            DataChangesRepository repository,
            GroupMembershipService gmService,
            GroupService groupService,
            InvitationService invitationService,
            MediafileService mediafileService,
            OrganizationMembershipService omService,
            OrganizationService organizationService,
            PassageService passageService,
            PassageStateChangeService passageStateChangeService,
            PlanService planService,
            ProjectIntegrationService piService,
            ProjectService projectService,
            SectionService sectionService,
            UserService userService,
            CurrentUserRepository currentUserRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, repository, loggerFactory)
        {
            CurrentUserContext = currentUserContext;
            GMService = gmService;
            GroupService = groupService;
            InvitationService = invitationService;
            MediafileService = mediafileService;
            OrgMemService = omService;
            OrganizationService = organizationService;
            PassageService = passageService;
            PassageStateChangeService = passageStateChangeService;
            PlanService = planService;
            ProjIntService = piService;
            ProjectService = projectService;
            SectionService = sectionService;
            UserService = userService;
            CurrentUserRepository = currentUserRepository;

        }
        private User CurrentUser() { return CurrentUserRepository.GetCurrentUser().Result; }

        private void BuildList(IEnumerable<BaseModel> recs, string type, List<OrbitId[]> addTo, bool toEnd = true)
        {
            List<OrbitId> tblList = new List<OrbitId>();

            foreach (BaseModel m in recs)
            {
                tblList.Add(new OrbitId() { type = type, id = m.Id });
            }
            if (tblList.Count > 0)
                if (toEnd)
                    addTo.Add(tblList.ToArray());
                else
                    addTo.Insert(0, tblList.ToArray());
        }
        public DataChanges GetChanges(string origin, DateTime dtSince)
        {
            List<OrbitId[]> changes = new List<OrbitId[]>();
            List<OrbitId[]> deleted = new List<OrbitId[]>();
            DateTime dtNow = DateTime.UtcNow;
            int currentUser = CurrentUser().Id;
            
            BuildList(UserService.GetChanges(currentUser, origin, dtSince), "user", changes);
            BuildList(UserService.GetDeletedSince(currentUser, origin, dtSince), "user", deleted, false);
            
            BuildList(OrganizationService.GetChanges(currentUser, origin, dtSince), "organization", changes);
            BuildList(OrganizationService.GetDeletedSince(currentUser, origin, dtSince), "organization", deleted, false);
            
            BuildList(OrgMemService.GetChanges(currentUser, origin, dtSince), "organizationmembership", changes);
            BuildList(OrgMemService.GetDeletedSince(currentUser, origin, dtSince), "organizationmembership", deleted, false);
            
            BuildList(GroupService.GetChanges(currentUser, origin, dtSince), "group", changes);
            BuildList(GroupService.GetDeletedSince(currentUser, origin, dtSince), "group", deleted, false);
            
            BuildList(GMService.GetChanges(currentUser, origin, dtSince), "groupmembership", changes);
            BuildList(GMService.GetDeletedSince(currentUser, origin, dtSince), "groupmembership", deleted, false);
            
            BuildList(ProjectService.GetChanges(currentUser, origin, dtSince), "project", changes);
            BuildList(ProjectService.GetDeletedSince(currentUser, origin, dtSince), "project", deleted, false);
            
            BuildList(PlanService.GetChanges(currentUser, origin, dtSince), "plan", changes);
            BuildList(PlanService.GetDeletedSince(currentUser, origin, dtSince), "plan", deleted, false);
            
            BuildList(SectionService.GetChanges(currentUser, origin, dtSince), "section", changes);
            BuildList(SectionService.GetDeletedSince(currentUser, origin, dtSince), "section", deleted, false);
            
            BuildList(PassageService.GetChanges(currentUser, origin, dtSince), "passage", changes);
            BuildList(PassageService.GetDeletedSince(currentUser, origin, dtSince), "passage", deleted, false);
            
            BuildList(MediafileService.GetChanges(currentUser, origin, dtSince), "mediafile", changes);
            BuildList(MediafileService.GetDeletedSince(currentUser, origin, dtSince), "mediafile", deleted, false);
            
            BuildList(PassageStateChangeService.GetChanges(currentUser, origin, dtSince), "passagestatechange", changes);
            
            BuildList(ProjIntService.GetChanges(currentUser, origin, dtSince), "projectintegration", changes);
            //BuildList(ProjIntService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "projectintegration", deleted);
            
            BuildList(InvitationService.GetChanges(currentUser, origin, dtSince), "invitation", changes);
            //BuildList(InvitationService.GetDeleted().Result.Where(p => (p.LastModifiedBy != CurrentUser.Id || p.LastModifiedOrigin != HttpContext.GetOrigin()) && p.DateUpdated > dtSince), "invitation", deleted);

            DataChanges ret = new DataChanges() { Id = 1, Querydate = dtNow, Changes = changes.ToArray(), Deleted = deleted.ToArray() };
            return ret;
        }
    }
}
