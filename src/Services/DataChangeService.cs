using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;

namespace SIL.Transcriber.Services
{
    public class ProjDate
    {
        public int id;
        public DateTime since;
    }

    public class DataChangeService : BaseService<Datachanges>
    {
        public ICurrentUserContext CurrentUserContext { get; }
        private readonly ArtifactCategoryService ArtifactCategoryService;
        private readonly ArtifactTypeService ArtifactTypeService;
        private readonly CommentService CommentService;
        private readonly DiscussionService DiscussionService;
        private readonly GroupMembershipService GMService;
        private readonly GroupService GroupService;
        private readonly IntellectualPropertyService IntellectualPropertyService;
        private readonly InvitationService InvitationService;
        private readonly MediafileService MediafileService;
        private readonly OrganizationMembershipService OrgMemService;
        private readonly OrganizationService OrganizationService;
        private readonly OrgKeytermService OrgKeytermService;
        private readonly OrgKeytermReferenceService OrgKeytermReferenceService;
        private readonly OrgKeytermTargetService OrgKeytermTargetService;
        private readonly OrgWorkflowStepService OrgWorkflowStepService;
        private readonly PassageService PassageService;
        private readonly PassageStateChangeService PassageStateChangeService;
        private readonly PlanService PlanService;
        private readonly ProjectIntegrationService ProjIntService;
        private readonly ProjectService ProjectService;
        private readonly SectionResourceService SectionResourceService;
        private readonly SectionResourceUserService SectionResourceUserService;
        private readonly SectionService SectionService;
        private readonly SharedResourceService SharedResourceService;
        private readonly SharedResourceReferenceService SharedResourceReferenceService;
        private readonly UserService UserService;
        private readonly WorkflowStepService WorkflowStepService;
        private readonly CurrentUserRepository CurrentUserRepository;
        private readonly List<OrbitId> changes = new();
        private readonly List<OrbitId> deleted = new();
        protected readonly AppDbContext dbContext;

        public DataChangeService(
            IResourceRepositoryAccessor repositoryAccessor,
            IQueryLayerComposer queryLayerComposer,
            IPaginationContext paginationContext,
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IJsonApiRequest request,
            IResourceChangeTracker<Datachanges> resourceChangeTracker,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            ICurrentUserContext currentUserContext,
            DatachangesRepository repository,
            ArtifactCategoryService artifactCategoryService,
            ArtifactTypeService artifactTypeService,
            CommentService commentService,
            DiscussionService discussionService,
            GroupMembershipService gmService,
            GroupService groupService,
            IntellectualPropertyService intellectualPropertyService,
            InvitationService invitationService,
            MediafileService mediafileService,
            OrganizationMembershipService omService,
            OrganizationService organizationService,
            OrgWorkflowStepService orgWorkflowStepService,
            OrgKeytermService orgKeytermService,
            OrgKeytermReferenceService orgKeytermReferenceService,
            OrgKeytermTargetService orgKeytermTargetService,
            PassageService passageService,
            PassageStateChangeService passageStateChangeService,
            PlanService planService,
            ProjectIntegrationService piService,
            ProjectService projectService,
            SectionService sectionService,
            SectionResourceService sectionResourceService,
            SectionResourceUserService sectionResourceUserService,
            SharedResourceService sharedResourceService,
            SharedResourceReferenceService sharedResourceReferenceService,
            UserService userService,
            WorkflowStepService workflowStepService,
            CurrentUserRepository currentUserRepository,
            AppDbContextResolver contextResolver
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
            dbContext = (AppDbContext)contextResolver.GetContext();
            CurrentUserContext = currentUserContext;
            ArtifactCategoryService = artifactCategoryService;
            ArtifactTypeService = artifactTypeService;
            CommentService = commentService;
            DiscussionService = discussionService;
            GMService = gmService;
            GroupService = groupService;
            IntellectualPropertyService = intellectualPropertyService;
            InvitationService = invitationService;
            MediafileService = mediafileService;
            OrgMemService = omService;
            OrganizationService = organizationService;
            OrgKeytermService = orgKeytermService;
            OrgKeytermReferenceService = orgKeytermReferenceService;
            OrgKeytermTargetService = orgKeytermTargetService;
            OrgWorkflowStepService = orgWorkflowStepService;
            PassageService = passageService;
            PassageStateChangeService = passageStateChangeService;
            PlanService = planService;
            ProjIntService = piService;
            ProjectService = projectService;
            SectionService = sectionService;
            SectionResourceService = sectionResourceService;
            SectionResourceUserService = sectionResourceUserService;
            SharedResourceService = sharedResourceService;
            SharedResourceReferenceService = sharedResourceReferenceService;
            UserService = userService;
            WorkflowStepService = workflowStepService;
            CurrentUserRepository = currentUserRepository;
        }

        private User? CurrentUser()
        {
            return CurrentUserRepository.GetCurrentUser();
        }

        private static void BuildList(
            IEnumerable<BaseModel> recs,
            string type,
            List<OrbitId> addTo,
            bool toEnd = true
        )
        {
            OrbitId tblList = new(type);
            foreach (BaseModel m in recs)
            {
                tblList.Ids.Add(m.Id);
            }
            if (toEnd)
                addTo.Add(tblList);
            else
                addTo.Insert(0, tblList);
        }

        private static void AddNewChanges(List<OrbitId> newCh, List<OrbitId> master)
        {
            newCh.ForEach(t => {
                OrbitId? list = master.Find(c => c.Type == t.Type);
                if (list == null)
                    master.Add(t);
                else
                    list.AddUnique(t.Ids);
            });
        }

        public Datachanges GetProjectChanges(
            string origin,
            ProjDate? [] projects,
            string version = "1",
            int start = 0
        )
        {
            DateTime dtNow = DateTime.UtcNow;
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;
            List<OrbitId> changes = new();
            List<OrbitId> deleted = new();
            DCReturn? ret = null;
            //we don't pass in an array anymore but backward compatibility
            foreach (ProjDate? pd in projects)
            {
                if (pd != null)
                {
                    ret = GetChanges(origin, pd.since, 0, pd.id, dbVersion, start);
                    AddNewChanges(ret.changes, changes);
                    AddNewChanges(ret.deleted, deleted);
                    start = ret.startNext;
                }
            }
            _ = changes.RemoveAll(c => c.Ids.Count == 0);
            _ = deleted.RemoveAll(d => d.Ids.Count == 0);
            return new Datachanges()
            {
                Id = 1,
                Startnext = start,
                Querydate = dtNow,
                Changes = changes.ToArray(),
                Deleted = deleted.ToArray()
            };
        }

        public Datachanges? GetUserChanges(
            string origin,
            DateTime dtSince,
            string version = "1",
            int start = 0
        )
        {
            DateTime dtNow = DateTime.UtcNow;
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;
            try
            {
                User? user = CurrentUser();
                if (user == null)
                    return null;
                DCReturn? ret = null;
                ret = GetChanges(origin, dtSince, user.Id, 0, dbVersion, start);
                AddNewChanges(ret.changes, changes);
                AddNewChanges(ret.deleted, deleted);
                _ = changes.RemoveAll(c => c.Ids.Count == 0);
                _ = deleted.RemoveAll(d => d.Ids.Count == 0);
                return new Datachanges()
                {
                    Id = 1,
                    Startnext = ret.startNext,
                    Querydate = dtNow,
                    Changes = changes.ToArray(),
                    Deleted = deleted.ToArray()
                };
            }
            catch
            {
                return null;
            }
        }

        private class ArchiveModel : BaseModel, IArchive
        {
            bool IArchive.Archived {
                get => throw new NotImplementedException();
                set => throw new NotImplementedException();
            }
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

        private static int CheckStart(DateTime dtBail, int completed)
        {
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return 1000;
            return completed;
        }

        // csharpier-ignore
        private DCReturn GetChanges(
            string origin,
            DateTime dtSince,
            int currentUser,
            int project,
            int dbVersion,
            int start
        )
        {
            Logger.LogInformation("GetChanges {start} {dtSince} {project}", start, dtSince, project);
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            int LAST_ADD = (dbVersion > 5) ? 26 : (dbVersion > 3) ? 21 : 12;
            int startNext = start;
            if (CheckStart(dtBail, startNext) == 0)
            {
                BuildList(UserService.GetChanges(dbContext.Users, currentUser, origin, dtSince, project), "user", changes);
                BuildList(UserService.GetDeletedSince(dbContext.Users, currentUser, origin, dtSince), "user", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 1)
            {
                BuildList(OrganizationService.GetChanges(dbContext.Organizations, currentUser, origin, dtSince, project), "organization", changes);
                BuildList(OrganizationService.GetDeletedSince(dbContext.Organizations, currentUser, origin, dtSince), "organization", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 2)
            {
                BuildList(OrgMemService.GetChanges(dbContext.Organizationmemberships, currentUser, origin, dtSince, project), "organizationmembership", changes);
                BuildList(OrgMemService.GetDeletedSince(dbContext.Organizationmemberships, currentUser, origin, dtSince), "organizationmembership", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 3)
            {
                BuildList(GroupService.GetChanges(dbContext.Groups, currentUser, origin, dtSince, project), "group", changes);
                BuildList(GroupService.GetDeletedSince(dbContext.Groups, currentUser, origin, dtSince), "group", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 4)
            {
                BuildList(GMService.GetChanges(dbContext.Groupmemberships, currentUser, origin, dtSince, project), "groupmembership", changes);
                BuildList(GMService.GetDeletedSince(dbContext.Groupmemberships, currentUser, origin, dtSince), "groupmembership", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 5)
            {
                BuildList(ProjectService.GetChanges(dbContext.Projects, currentUser, origin, dtSince, project), "project", changes);
                BuildList(ProjectService.GetDeletedSince(dbContext.Projects, currentUser, origin, dtSince), "project", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 6)
            {
                BuildList(PlanService.GetChanges(dbContext.Plans, currentUser, origin, dtSince, project), "plan", changes);
                BuildList(PlanService.GetDeletedSince(dbContext.Plans, currentUser, origin, dtSince), "plan", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 7)
            {
                BuildList(SectionService.GetChanges(dbContext.Sections, currentUser, origin, dtSince, project), "section", changes);
                BuildList(SectionService.GetDeletedSince(dbContext.Sections, currentUser, origin, dtSince), "section", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 8)
            {
                BuildList(PassageService.GetChanges(dbContext.Passages, currentUser, origin, dtSince, project), "passage", changes);
                BuildList(PassageService.GetDeletedSince(dbContext.Passages, currentUser, origin, dtSince), "passage", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 9)
            {
                BuildList(MediafileService.GetChanges(dbContext.Mediafiles, currentUser, origin, dtSince, project), "mediafile", changes);
                BuildList(MediafileService.GetDeletedSince(dbContext.Mediafiles, currentUser, origin, dtSince), "mediafile", deleted, false);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 10)
            {
                BuildList(PassageStateChangeService.GetChanges(dbContext.Passagestatechanges, currentUser, origin, dtSince, project), "passagestatechange", changes);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 11)
            {
                BuildList(ProjIntService.GetChanges(dbContext.Projectintegrations, currentUser, origin, dtSince, project), "projectintegration", changes);
                startNext++;
            }
            if (CheckStart(dtBail, startNext) == 12)
            {
                BuildList(InvitationService.GetChanges(dbContext.Invitations, currentUser, origin, dtSince, project), "invitation", changes);
                startNext++;
            }
            if (dbVersion > 3)
            {
                if (CheckStart(dtBail, startNext) == 13)
                {
                    BuildList(ArtifactCategoryService.GetChanges(dbContext.Artifactcategorys, currentUser, origin, dtSince, project), "artifactcategory", changes);
                    BuildList(ArtifactCategoryService.GetDeletedSince(dbContext.Artifactcategorys, currentUser, origin, dtSince), "artifactcategory", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 14)
                {
                    BuildList(ArtifactTypeService.GetChanges(dbContext.Artifacttypes, currentUser, origin, dtSince, project), "artifacttype", changes);
                    BuildList(ArtifactTypeService.GetDeletedSince(dbContext.Artifacttypes, currentUser, origin, dtSince), "artifacttype", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 15)
                {
                    BuildList(DiscussionService.GetChanges(dbContext.Discussions, currentUser, origin, dtSince, project), "discussion", changes);
                    BuildList(DiscussionService.GetDeletedSince(dbContext.Discussions, currentUser, origin, dtSince), "discussion", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 16)
                {
                    BuildList(CommentService.GetChanges(dbContext.Comments, currentUser, origin, dtSince, project), "comment", changes);
                    BuildList(CommentService.GetDeletedSince(dbContext.Comments, currentUser, origin, dtSince), "comment", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 17)
                {
                    BuildList(OrgWorkflowStepService.GetChanges(dbContext.Orgworkflowsteps, currentUser, origin, dtSince, project), "orgworkflowstep", changes);
                    BuildList(OrgWorkflowStepService.GetDeletedSince(dbContext.Orgworkflowsteps, currentUser, origin, dtSince), "orgworkflowstep", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 18)
                {
                    BuildList(SectionResourceService.GetChanges(dbContext.Sectionresources, currentUser, origin, dtSince, project), "sectionresource", changes);
                    BuildList(SectionResourceService.GetDeletedSince(dbContext.Sectionresources, currentUser, origin, dtSince), "sectionresource", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 19)
                {
                    BuildList(SectionResourceUserService.GetChanges(dbContext.Sectionresourceusers, currentUser, origin, dtSince, project), "sectionresourceuser", changes);
                    BuildList(SectionResourceUserService.GetDeletedSince(dbContext.Sectionresourceusers, currentUser, origin, dtSince), "sectionresourceuser", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 20)
                {
                    BuildList(WorkflowStepService.GetChanges(dbContext.Workflowsteps, currentUser, origin, dtSince, project), "workflowstep", changes);
                    BuildList(WorkflowStepService.GetDeletedSince(dbContext.Workflowsteps, currentUser, origin, dtSince), "workflowstep", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 21)
                {
                    BuildList(IntellectualPropertyService.GetChanges(dbContext.IntellectualPropertys, currentUser, origin, dtSince, project), "intellectualproperty", changes);
                    BuildList(IntellectualPropertyService.GetDeletedSince(dbContext.IntellectualPropertys, currentUser, origin, dtSince), "intellectualproperty", deleted, false);
                    startNext++;
                }
            }
            if (dbVersion > 5)
            { 
                if (CheckStart(dtBail, startNext) == 22)
                {
                    BuildList(OrgKeytermService.GetChanges(dbContext.Orgkeyterms, currentUser, origin, dtSince, project), "orgkeyterm", changes);
                    BuildList(OrgKeytermService.GetDeletedSince(dbContext.Orgkeyterms, currentUser, origin, dtSince), "orgkeyterm", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 23)
                {
                    BuildList(OrgKeytermReferenceService.GetChanges(dbContext.Orgkeytermreferences, currentUser, origin, dtSince, project), "orgkeytermreference", changes);
                    BuildList(OrgKeytermReferenceService.GetDeletedSince(dbContext.Orgkeytermreferences, currentUser, origin, dtSince), "orgkeytermreference", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 24)
                {
                    BuildList(OrgKeytermTargetService.GetChanges(dbContext.Orgkeytermtargets, currentUser, origin, dtSince, project), "orgkeytermtarget", changes);
                    BuildList(OrgKeytermTargetService.GetDeletedSince(dbContext.Orgkeytermtargets, currentUser, origin, dtSince), "orgkeytermtarget", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == 25)
                {
                    BuildList(SharedResourceService.GetChanges(dbContext.Sharedresources, currentUser, origin, dtSince, project), "sharedresource", changes);
                    BuildList(SharedResourceService.GetDeletedSince(dbContext.Sharedresources, currentUser, origin, dtSince), "sharedresource", deleted, false);
                    startNext++;
                }
                if (CheckStart(dtBail, startNext) == LAST_ADD)
                {
                    BuildList(SharedResourceReferenceService.GetChanges(dbContext.Sharedresourcereferences, currentUser, origin, dtSince, project), "sharedresourcereference", changes);
                    BuildList(SharedResourceReferenceService.GetDeletedSince(dbContext.Sharedresourcereferences, currentUser, origin, dtSince), "sharedresourcereference", deleted, false);
                    startNext++;
                }
            }
            DCReturn ret = new (changes, deleted, startNext > LAST_ADD ? -1 : startNext);
            return ret;
        }
    }
}
