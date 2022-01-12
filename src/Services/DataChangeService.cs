using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System;
using System.Collections.Generic;
using TranscriberAPI;

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

        private readonly ArtifactCategoryService ArtifactCategoryService;
        private readonly ArtifactTypeService ArtifactTypeService;
        private readonly CommentService CommentService;
        private readonly DiscussionService DiscussionService;
        private readonly GroupMembershipService GMService;
        private readonly GroupService GroupService;
        private readonly InvitationService InvitationService;
        private readonly MediafileService MediafileService;
        private readonly OrganizationMembershipService OrgMemService;
        private readonly OrganizationService OrganizationService;
        private readonly OrgWorkflowStepService OrgWorkflowStepService;
        private readonly PassageService PassageService;
        private readonly PassageStateChangeService PassageStateChangeService;
        private readonly PlanService PlanService;
        private readonly ProjectIntegrationService ProjIntService;
        private readonly ProjectService ProjectService;
        private readonly SectionResourceService SectionResourceService;
        private readonly SectionResourceUserService SectionResourceUserService;
        private readonly SectionService SectionService;
        private readonly UserService UserService;
        private readonly WorkflowStepService WorkflowStepService;
        private readonly CurrentUserRepository CurrentUserRepository;
        List<OrbitId> changes = new List<OrbitId>();
        List<OrbitId> deleted = new List<OrbitId>();

        public DataChangeService(
            IJsonApiContext jsonApiContext,
            ICurrentUserContext currentUserContext,
            DataChangesRepository repository,
            ArtifactCategoryService artifactCategoryService,
            ArtifactTypeService artifactTypeService,
            CommentService commentService,
            DiscussionService discussionService,
            GroupMembershipService gmService,
            GroupService groupService,
            InvitationService invitationService,
            MediafileService mediafileService,
            OrganizationMembershipService omService,
            OrganizationService organizationService,
            OrgWorkflowStepService orgWorkflowStepService,
            PassageService passageService,
            PassageStateChangeService passageStateChangeService,
            PlanService planService,
            ProjectIntegrationService piService,
            ProjectService projectService,
            SectionService sectionService,
            SectionResourceService sectionResourceService,
            SectionResourceUserService sectionResourceUserService,
            UserService userService,
            WorkflowStepService workflowStepService,
            CurrentUserRepository currentUserRepository,
            ILoggerFactory loggerFactory) : base(jsonApiContext, repository, loggerFactory)
        {
            CurrentUserContext = currentUserContext;
            ArtifactCategoryService = artifactCategoryService;
            ArtifactTypeService = artifactTypeService;
            CommentService = commentService;
            DiscussionService = discussionService;
            GMService = gmService;
            GroupService = groupService;
            InvitationService = invitationService;
            MediafileService = mediafileService;
            OrgMemService = omService;
            OrganizationService = organizationService;
            OrgWorkflowStepService = orgWorkflowStepService;
            PassageService = passageService;
            PassageStateChangeService = passageStateChangeService;
            PlanService = planService;
            ProjIntService = piService;
            ProjectService = projectService;
            SectionService = sectionService;
            SectionResourceService = sectionResourceService;
            SectionResourceUserService = sectionResourceUserService;
            UserService = userService;
            WorkflowStepService = workflowStepService;
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

        public DataChanges GetProjectChanges(string origin, ProjDate[] projects, string version = "1", int start = 0)
        {
            DateTime dtNow = DateTime.UtcNow;
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;
            List<OrbitId> changes = new List<OrbitId>();
            List < OrbitId > deleted = new List<OrbitId>();
            DCReturn ret=null;
            //we don't pass in an array anymore but backward compatibility
            foreach (ProjDate pd in projects)
            {
                ret = GetChanges(origin, pd.since, 0, pd.id, dbVersion, start);
                AddNewChanges(ret.changes, changes);
                AddNewChanges(ret.deleted, deleted);
            }
            changes.RemoveAll(c => c.ids.Count == 0);
            deleted.RemoveAll(d => d.ids.Count == 0);
            
            return new DataChanges() { Id = 1, Querydate = dtNow, Startnext=ret != null ? ret.startNext : -1, Changes = changes.ToArray(), Deleted = deleted.ToArray() };
        }
        public DataChanges GetUserChanges(string origin, DateTime dtSince, string version="1", int start = 0)
        {
            DateTime dtNow = DateTime.UtcNow;
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;
            DCReturn ret = GetChanges(origin, dtSince, CurrentUser().Id, 0, dbVersion,start);
            ret.changes.RemoveAll(c => c.ids.Count == 0);
            ret.deleted.RemoveAll(d => d.ids.Count == 0);
            return new DataChanges() { Id = 1, Querydate = dtNow, Startnext = ret.startNext, Changes = ret.changes.ToArray(), Deleted = ret.deleted.ToArray() };
        }
        private class ArchiveModel : BaseModel, IArchive
        {
            bool IArchive.Archived { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        }

        private class DCReturn
        {
            public int startNext;
            public List<OrbitId> changes;
            public List<OrbitId> deleted;

            public DCReturn(List<OrbitId> Changes, List<OrbitId> Deleted, int StartNext)
            {
                changes = Changes;
                deleted = Deleted;
                startNext = StartNext;
            }
        }

        private int CheckStart(int check, DateTime dtBail, int completed)
        {
            Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail) return 1000;
            return completed;
        }
        private DCReturn GetChanges(string origin, DateTime dtSince, int currentUser, int project, int dbVersion, int start)
        {
            Logger.LogInformation($"GetChanges {start} {dtSince} {project}");
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            const int LAST_ADD = 20;
            int startNext = start;
            if (CheckStart(0, dtBail, startNext) == 0) {
                BuildList(UserService.GetChanges(currentUser, origin, dtSince, project), "user", changes);
                BuildList(UserService.GetDeletedSince(currentUser, origin, dtSince), "user", deleted, false);
                startNext++;
            }
            if (CheckStart(1, dtBail, startNext) == 1) {
                BuildList(OrganizationService.GetChanges(currentUser, origin, dtSince, project), "organization", changes);
                BuildList(OrganizationService.GetDeletedSince(currentUser, origin, dtSince), "organization", deleted, false);
                startNext++;
            }
            if (CheckStart(2, dtBail, startNext) == 2) {
                BuildList(OrgMemService.GetChanges(currentUser, origin, dtSince, project), "organizationmembership", changes);
                BuildList(OrgMemService.GetDeletedSince(currentUser, origin, dtSince), "organizationmembership", deleted, false);
                startNext++;
            }
            if (CheckStart(3, dtBail, startNext) == 3) {
                BuildList(GroupService.GetChanges(currentUser, origin, dtSince, project), "group", changes);
                BuildList(GroupService.GetDeletedSince(currentUser, origin, dtSince), "group", deleted, false);
                startNext++;
            }
            if (CheckStart(4, dtBail, startNext) == 4) {
                BuildList(GMService.GetChanges(currentUser, origin, dtSince, project), "groupmembership", changes);
                BuildList(GMService.GetDeletedSince(currentUser, origin, dtSince), "groupmembership", deleted, false);
                startNext++;
            }
            if (CheckStart(5, dtBail, startNext) == 5) {
                BuildList(ProjectService.GetChanges(currentUser, origin, dtSince, project), "project", changes);
                BuildList(ProjectService.GetDeletedSince(currentUser, origin, dtSince), "project", deleted, false);
                startNext++;
            }
            if (CheckStart(6, dtBail, startNext) == 6) { 
                BuildList(PlanService.GetChanges(currentUser, origin, dtSince, project), "plan", changes);
                BuildList(PlanService.GetDeletedSince(currentUser, origin, dtSince), "plan", deleted, false);
                startNext++;
            }
            if (CheckStart(7, dtBail, startNext) == 7)
            {
                BuildList(SectionService.GetChanges(currentUser, origin, dtSince, project), "section", changes);
                BuildList(SectionService.GetDeletedSince(currentUser, origin, dtSince), "section", deleted, false);
                startNext++;
            }
            if (CheckStart(8, dtBail, startNext) == 8)
            {
                BuildList(PassageService.GetChanges(currentUser, origin, dtSince, project), "passage", changes);
                BuildList(PassageService.GetDeletedSince(currentUser, origin, dtSince), "passage", deleted, false);
                startNext++;
            }
            if (CheckStart(9, dtBail, startNext) == 9)
            {
                BuildList(MediafileService.GetChanges(currentUser, origin, dtSince, project), "mediafile", changes);
                BuildList(MediafileService.GetDeletedSince(currentUser, origin, dtSince), "mediafile", deleted, false);
                startNext++;
            }
            if (CheckStart(10, dtBail, startNext) == 10)
            {
                BuildList(PassageStateChangeService.GetChanges(currentUser, origin, dtSince, project), "passagestatechange", changes);
                startNext++;
            }
            if (CheckStart(11, dtBail, startNext) == 11)
            {
                BuildList(ProjIntService.GetChanges(currentUser, origin, dtSince, project), "projectintegration", changes);
                startNext++;
            }
            if (CheckStart(12, dtBail, startNext) == 12)
            {
                BuildList(InvitationService.GetChanges(currentUser, origin, dtSince, project), "invitation", changes);
                startNext++;
            }
            if (dbVersion > 3)
            {
                if (CheckStart(13, dtBail, startNext) == 13)
                {
                    BuildList(ArtifactCategoryService.GetChanges(currentUser, origin, dtSince, project), "artifactcategory", changes);
                    BuildList(ArtifactCategoryService.GetDeletedSince(currentUser, origin, dtSince), "artifactcategory", deleted, false);
                    startNext++;
                }
                if (CheckStart(14, dtBail, startNext) == 14)
                {
                    BuildList(ArtifactTypeService.GetChanges(currentUser, origin, dtSince, project), "artifacttype", changes);
                    BuildList(ArtifactTypeService.GetDeletedSince(currentUser, origin, dtSince), "artifacttype", deleted, false);
                    startNext++;
                }
                if (CheckStart(15, dtBail, startNext) == 15)
                {
                    BuildList(DiscussionService.GetChanges(currentUser, origin, dtSince, project), "discussion", changes);
                    BuildList(DiscussionService.GetDeletedSince(currentUser, origin, dtSince), "discussion", deleted, false);
                    startNext++;
                }
                if (CheckStart(16, dtBail, startNext) == 16)
                {
                    BuildList(CommentService.GetChanges(currentUser, origin, dtSince, project), "comment", changes);
                    BuildList(CommentService.GetDeletedSince(currentUser, origin, dtSince), "comment", deleted, false);
                    startNext++;
                }
                if (CheckStart(17, dtBail,  startNext) == 17)
                {
                    BuildList(OrgWorkflowStepService.GetChanges(currentUser, origin, dtSince, project), "orgworkflowstep", changes);
                    BuildList(OrgWorkflowStepService.GetDeletedSince(currentUser, origin, dtSince), "orgworkflowstep", deleted, false);
                    startNext++;
                }
                if (CheckStart(18, dtBail,  startNext) == 18)
                {
                    BuildList(SectionResourceService.GetChanges(currentUser, origin, dtSince, project), "sectionresource", changes);
                    BuildList(SectionResourceService.GetDeletedSince(currentUser, origin, dtSince), "sectionresource", deleted, false);
                    startNext++;
                }
                if (CheckStart(19, dtBail,  startNext) == 19)
                {
                    BuildList(SectionResourceUserService.GetChanges(currentUser, origin, dtSince, project), "sectionresourceuser", changes);
                    BuildList(SectionResourceUserService.GetDeletedSince(currentUser, origin, dtSince), "sectionresourceuser", deleted, false);
                    startNext++;
                }
                if (CheckStart(LAST_ADD, dtBail,  startNext) == LAST_ADD)
                {
                    BuildList(WorkflowStepService.GetChanges(currentUser, origin, dtSince, project), "workflowstep", changes);
                    BuildList(WorkflowStepService.GetDeletedSince(currentUser, origin, dtSince), "workflowstep", deleted, false);
                    startNext++;
                }
            }
            DCReturn ret = new DCReturn( changes, deleted, startNext > LAST_ADD ? -1 : startNext);
            return  ret;
        }
    }
}

