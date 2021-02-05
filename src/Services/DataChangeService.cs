using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Services
{
    public class ProjDate
    {
        public int id;
        public DateTime since;
    }
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

        private void BuildList(IEnumerable<BaseModel> recs, string type, List<OrbitId> addTo, bool toEnd = true)
        {
            OrbitId tblList = new OrbitId(type);
            foreach (BaseModel m in recs)
            {
                tblList.ids.Add(m.Id);
            }
            if (toEnd)
                addTo.Add(tblList);
            else
               addTo.Insert(0, tblList);
        }
        private void AddNewChanges(List<OrbitId> newCh, List<OrbitId> master)
        {
            newCh.ForEach(t =>
            {
                OrbitId list = master.Find(c => c.type == t.type);
                if (list == null)
                    master.Add(t);
                else
                    list.AddUnique(t.ids);
            });
        }
        public DataChanges GetProjectChanges(string origin, ProjDate[] projects)
        {
            DateTime dtNow = DateTime.UtcNow;
            List<OrbitId> changes = new List<OrbitId>();
            List<OrbitId> deleted = new List<OrbitId>();
            foreach(ProjDate pd in projects)
            {
                List<OrbitId>[] ret = GetChanges(origin, pd.since, 0, pd.id);
                AddNewChanges(ret[0], changes);
                AddNewChanges(ret[1], deleted);
            }
            changes.RemoveAll(c => c.ids.Count == 0);
            deleted.RemoveAll(d => d.ids.Count == 0);
            return new DataChanges() { Id = 1, Querydate = dtNow, Changes = changes.ToArray(), Deleted = deleted.ToArray() };
        }
        public DataChanges GetUserChanges(string origin, DateTime dtSince)
        {
            DateTime dtNow = DateTime.UtcNow;
            List<OrbitId>[] ret = GetChanges(origin, dtSince, CurrentUser().Id, 0);
            ret[0].RemoveAll(c => c.ids.Count == 0);
            ret[1].RemoveAll(d => d.ids.Count == 0);
            return new DataChanges() { Id = 1, Querydate = dtNow, Changes = ret[0].ToArray(), Deleted = ret[1].ToArray() };
        }
        private List<OrbitId>[] GetChanges(string origin, DateTime dtSince, int currentUser, int project)
        {
            List<OrbitId> changes = new List<OrbitId>();
            List<OrbitId> deleted = new List<OrbitId>();
            
            BuildList(UserService.GetChanges(currentUser, origin, dtSince, project), "user", changes);
            BuildList(UserService.GetDeletedSince(currentUser, origin, dtSince), "user", deleted, false);
            
            BuildList(OrganizationService.GetChanges(currentUser, origin, dtSince, project), "organization", changes);
            BuildList(OrganizationService.GetDeletedSince(currentUser, origin, dtSince), "organization", deleted, false);
            
            BuildList(OrgMemService.GetChanges(currentUser, origin, dtSince, project), "organizationmembership", changes);
            BuildList(OrgMemService.GetDeletedSince(currentUser, origin, dtSince), "organizationmembership", deleted, false);
            
            BuildList(GroupService.GetChanges(currentUser, origin, dtSince, project), "group", changes);
            BuildList(GroupService.GetDeletedSince(currentUser, origin, dtSince), "group", deleted, false);
            
            BuildList(GMService.GetChanges(currentUser, origin, dtSince, project), "groupmembership", changes);
            BuildList(GMService.GetDeletedSince(currentUser, origin, dtSince), "groupmembership", deleted, false);
            
            BuildList(ProjectService.GetChanges(currentUser, origin, dtSince, project), "project", changes);
            BuildList(ProjectService.GetDeletedSince(currentUser, origin, dtSince), "project", deleted, false);
            
            BuildList(PlanService.GetChanges(currentUser, origin, dtSince, project), "plan", changes);
            BuildList(PlanService.GetDeletedSince(currentUser, origin, dtSince), "plan", deleted, false);
            
            BuildList(SectionService.GetChanges(currentUser, origin, dtSince, project), "section", changes);
            BuildList(SectionService.GetDeletedSince(currentUser, origin, dtSince), "section", deleted, false);
            
            BuildList(PassageService.GetChanges(currentUser, origin, dtSince, project), "passage", changes);
            BuildList(PassageService.GetDeletedSince(currentUser, origin, dtSince), "passage", deleted, false);
            
            BuildList(MediafileService.GetChanges(currentUser, origin, dtSince, project), "mediafile", changes);
            BuildList(MediafileService.GetDeletedSince(currentUser, origin, dtSince), "mediafile", deleted, false);
            
            BuildList(PassageStateChangeService.GetChanges(currentUser, origin, dtSince, project), "passagestatechange", changes);
            
            BuildList(ProjIntService.GetChanges(currentUser, origin, dtSince, project), "projectintegration", changes);
            //BuildList(ProjIntService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "projectintegration", deleted);
            
            BuildList(InvitationService.GetChanges(currentUser, origin, dtSince, project), "invitation", changes);
            //BuildList(InvitationService.GetDeleted().Result.Where(p => (p.LastModifiedBy != CurrentUser.Id || p.LastModifiedOrigin != HttpContext.GetOrigin()) && p.DateUpdated > dtSince), "invitation", deleted);
            List<OrbitId>[] ret = new List<OrbitId>[] { changes, deleted };
            return  ret;
        }
    }
}
