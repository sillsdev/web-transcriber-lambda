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
        public DataChanges GetProjectChanges(string origin, ProjDate[] projects, string version = "1")
        {
            DateTime dtNow = DateTime.UtcNow;
            List<OrbitId> changes = new List<OrbitId>();
            List<OrbitId> deleted = new List<OrbitId>();
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;

            foreach(ProjDate pd in projects)
            {
                List<OrbitId>[] ret = GetChanges(origin, pd.since, 0, pd.id, dbVersion );
                AddNewChanges(ret[0], changes);
                AddNewChanges(ret[1], deleted);
            }
            changes.RemoveAll(c => c.ids.Count == 0);
            deleted.RemoveAll(d => d.ids.Count == 0);
            return new DataChanges() { Id = 1, Querydate = dtNow, Changes = changes.ToArray(), Deleted = deleted.ToArray() };
        }
        public DataChanges GetUserChanges(string origin, DateTime dtSince, string version="1")
        {
            DateTime dtNow = DateTime.UtcNow;
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;
            List<OrbitId>[] ret = GetChanges(origin, dtSince, CurrentUser().Id, 0, dbVersion);
            ret[0].RemoveAll(c => c.ids.Count == 0);
            ret[1].RemoveAll(d => d.ids.Count == 0);
            return new DataChanges() { Id = 1, Querydate = dtNow, Changes = ret[0].ToArray(), Deleted = ret[1].ToArray() };
        }
        private List<OrbitId>[] GetChanges(string origin, DateTime dtSince, int currentUser, int project, int dbVersion)
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

            if (dbVersion > 3)
            {
                BuildList(ArtifactCategoryService.GetChanges(currentUser, origin, dtSince, project), "artifactcategory", changes);
                BuildList(ArtifactCategoryService.GetDeletedSince(currentUser, origin, dtSince), "artifactcategory", deleted, false);
                
                BuildList(ArtifactTypeService.GetChanges(currentUser, origin, dtSince, project), "artifacttype", changes);
                BuildList(ArtifactTypeService.GetDeletedSince(currentUser, origin, dtSince), "artifacttype", deleted, false);
                
                BuildList(CommentService.GetChanges(currentUser, origin, dtSince, project), "comment", changes);
                BuildList(CommentService.GetDeletedSince(currentUser, origin, dtSince), "comment", deleted, false);

                BuildList(DiscussionService.GetChanges(currentUser, origin, dtSince, project), "discussion", changes);
                BuildList(DiscussionService.GetDeletedSince(currentUser, origin, dtSince), "discussion", deleted, false);

                BuildList(DiscussionService.GetChanges(currentUser, origin, dtSince, project), "discussion", changes);
                BuildList(DiscussionService.GetDeletedSince(currentUser, origin, dtSince), "discussion", deleted, false);

                BuildList(OrgWorkflowStepService.GetChanges(currentUser, origin, dtSince, project), "orgworkflowstep", changes);
                BuildList(OrgWorkflowStepService.GetDeletedSince(currentUser, origin, dtSince), "orgworkflowstep", deleted, false);

                BuildList(SectionResourceService.GetChanges(currentUser, origin, dtSince, project), "sectionresource", changes);
                BuildList(SectionResourceService.GetDeletedSince(currentUser, origin, dtSince), "sectionresource", deleted, false);

                BuildList(SectionResourceUserService.GetChanges(currentUser, origin, dtSince, project), "sectionresourceuser", changes);
                BuildList(SectionResourceUserService.GetDeletedSince(currentUser, origin, dtSince), "sectionresourceuser", deleted, false);

                BuildList(WorkflowStepService.GetChanges(currentUser, origin, dtSince, project), "workflowstep", changes);
                BuildList(WorkflowStepService.GetDeletedSince(currentUser, origin, dtSince), "workflowstep", deleted, false);
            }
            List<OrbitId>[] ret = new List<OrbitId>[] { changes, deleted };

            return  ret;
        }
    }
}
