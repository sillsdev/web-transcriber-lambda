using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Utility;

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
        private readonly GraphicService GraphicService; 
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
            GraphicService graphicService,
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
            GraphicService = graphicService;
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

        private static int BuildList(
            IEnumerable<BaseModel> recs,
            string table,
            List<OrbitId> addTo,
            bool toEnd = true
        )
        {   //remove trailing s from table
            string type = Tables.ToType(table);
            OrbitId tblList = new(type);
            int ix = 0;
            int startId = -1; //where to start next time
            int stop = Math.Min(500, recs.Count());
            foreach(BaseModel m in recs)
            {
                if (ix > stop)
                {
                    startId = m.Id;
                    break;
                }
                tblList.Ids.Add(m.Id);
                ix++;
            }
            if (toEnd)
                addTo.Add(tblList);
            else
                addTo.Insert(0, tblList);
            return startId;
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
                    Project? x = dbContext.Projects.Find(pd.id) ?? throw new Exception("Project not found " + pd.id);
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
            return DateTime.Now > dtBail ? 1000 : completed;
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
            int LAST_ADD = (dbVersion > 6) ? 27 :(dbVersion > 5) ? 26 : (dbVersion > 3) ? 21 : 12;
            int startId = -1;
            start = StartIndex.GetStart(start, ref startId);

            if (CheckStart(dtBail, start) == 0)
            {
                BuildList(UserService.GetChanges(dbContext.Users, currentUser, origin, dtSince, project, startId), Tables.Users, changes);
                BuildList(UserService.GetDeletedSince(dbContext.Users, currentUser, origin, dtSince), Tables.Users, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 1)
            {
                BuildList(OrganizationService.GetChanges(dbContext.Organizations, currentUser, origin, dtSince, project, startId), Tables.Organizations, changes);
                BuildList(OrganizationService.GetDeletedSince(dbContext.Organizations, currentUser, origin, dtSince), Tables.Organizations, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 2)
            {
                BuildList(OrgMemService.GetChanges(dbContext.Organizationmemberships, currentUser, origin, dtSince, project, startId), Tables.OrganizationMemberships, changes);
                BuildList(OrgMemService.GetDeletedSince(dbContext.Organizationmemberships, currentUser, origin, dtSince), Tables.OrganizationMemberships, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 3)
            {
                BuildList(GroupService.GetChanges(dbContext.Groups, currentUser, origin, dtSince, project, startId), Tables.Groups, changes);
                BuildList(GroupService.GetDeletedSince(dbContext.Groups, currentUser, origin, dtSince), Tables.Groups, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 4)
            {
                BuildList(GMService.GetChanges(dbContext.Groupmemberships, currentUser, origin, dtSince, project, startId), Tables.GroupMemberships, changes);
                BuildList(GMService.GetDeletedSince(dbContext.Groupmemberships, currentUser, origin, dtSince), Tables.GroupMemberships, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 5)
            {
                BuildList(ProjectService.GetChanges(dbContext.Projects, currentUser, origin, dtSince, project, startId), Tables.Projects, changes);
                BuildList(ProjectService.GetDeletedSince(dbContext.Projects, currentUser, origin, dtSince), Tables.Projects, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 6)
            {
                BuildList(PlanService.GetChanges(dbContext.Plans, currentUser, origin, dtSince, project, startId), Tables.Plans, changes);
                BuildList(PlanService.GetDeletedSince(dbContext.Plans, currentUser, origin, dtSince), Tables.Plans, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 7)
            {
                BuildList(SectionService.GetChanges(dbContext.Sections, currentUser, origin, dtSince, project, startId), Tables.Sections, changes);
                BuildList(SectionService.GetDeletedSince(dbContext.Sections, currentUser, origin, dtSince), Tables.Sections, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 8)
            {
                BuildList(PassageService.GetChanges(dbContext.Passages, currentUser, origin, dtSince, project, startId), Tables.Passages, changes);
                BuildList(PassageService.GetDeletedSince(dbContext.Passages, currentUser, origin, dtSince), Tables.Passages, deleted, false);
                start++;
            }
            if (CheckStart(dtBail, start) == 9)
            {
                startId = BuildList(MediafileService.GetChanges(dbContext.Mediafiles, currentUser, origin, dtSince, project, startId), Tables.Mediafiles, changes);
                if (startId == -1)
                {
                    BuildList(MediafileService.GetDeletedSince(dbContext.Mediafiles, currentUser, origin, dtSince), Tables.Mediafiles, deleted, false);
                    start++;
                } else
                {
                    start = StartIndex.SetStart(start, ref startId);
                }
            }
            if (CheckStart(dtBail, start) == 10)
            {
                startId = BuildList(PassageStateChangeService.GetChanges(dbContext.Passagestatechanges, currentUser, origin, dtSince, project, startId), Tables.PassageStateChanges, changes);
                if (startId == -1)
                {
                    start++;
                }
                else
                {
                    start = StartIndex.SetStart(start, ref startId);
                }
            }
            if (CheckStart(dtBail, start) == 11)
            {
                BuildList(ProjIntService.GetChanges(dbContext.Projectintegrations, currentUser, origin, dtSince, project, startId), Tables.ProjectIntegrations, changes);
                start++;
            }
            if (CheckStart(dtBail, start) == 12)
            {
                BuildList(InvitationService.GetChanges(dbContext.Invitations, currentUser, origin, dtSince, project, startId), Tables.Invitations, changes);
                start++;
            }
            if (dbVersion > 3)
            {
                if (CheckStart(dtBail, start) == 13)
                {
                    BuildList(ArtifactCategoryService.GetChanges(dbContext.Artifactcategorys, currentUser, origin, dtSince, project, startId), Tables.ArtifactCategorys, changes);
                    BuildList(ArtifactCategoryService.GetDeletedSince(dbContext.Artifactcategorys, currentUser, origin, dtSince), Tables.ArtifactCategorys, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 14)
                {
                    BuildList(ArtifactTypeService.GetChanges(dbContext.Artifacttypes, currentUser, origin, dtSince, project, startId), Tables.ArtifactTypes, changes);
                    BuildList(ArtifactTypeService.GetDeletedSince(dbContext.Artifacttypes, currentUser, origin, dtSince), Tables.ArtifactTypes, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 15)
                {
                    BuildList(DiscussionService.GetChanges(dbContext.Discussions, currentUser, origin, dtSince, project, startId), Tables.Discussions, changes);
                    BuildList(DiscussionService.GetDeletedSince(dbContext.Discussions, currentUser, origin, dtSince), Tables.Discussions, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 16)
                {
                    BuildList(CommentService.GetChanges(dbContext.Comments, currentUser, origin, dtSince, project, startId), Tables.Comments, changes);
                    BuildList(CommentService.GetDeletedSince(dbContext.Comments, currentUser, origin, dtSince), Tables.Comments, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 17)
                {
                    BuildList(OrgWorkflowStepService.GetChanges(dbContext.Orgworkflowsteps, currentUser, origin, dtSince, project, startId), Tables.OrgWorkflowSteps, changes);
                    BuildList(OrgWorkflowStepService.GetDeletedSince(dbContext.Orgworkflowsteps, currentUser, origin, dtSince), Tables.OrgWorkflowSteps, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 18)
                {
                    BuildList(SectionResourceService.GetChanges(dbContext.Sectionresources, currentUser, origin, dtSince, project, startId), Tables.SectionResources, changes);
                    BuildList(SectionResourceService.GetDeletedSince(dbContext.Sectionresources, currentUser, origin, dtSince), Tables.SectionResources, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 19)
                {
                    BuildList(SectionResourceUserService.GetChanges(dbContext.Sectionresourceusers, currentUser, origin, dtSince, project, startId), Tables.SectionResourceUsers, changes);
                    BuildList(SectionResourceUserService.GetDeletedSince(dbContext.Sectionresourceusers, currentUser, origin, dtSince), Tables.SectionResourceUsers, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 20)
                {
                    BuildList(WorkflowStepService.GetChanges(dbContext.Workflowsteps, currentUser, origin, dtSince, project, startId), Tables.WorkflowSteps, changes);
                    BuildList(WorkflowStepService.GetDeletedSince(dbContext.Workflowsteps, currentUser, origin, dtSince), Tables.WorkflowSteps, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 21)
                {
                    BuildList(IntellectualPropertyService.GetChanges(dbContext.IntellectualPropertys, currentUser, origin, dtSince, project, startId), Tables.IntellectualPropertys, changes);
                    BuildList(IntellectualPropertyService.GetDeletedSince(dbContext.IntellectualPropertys, currentUser, origin, dtSince), Tables.IntellectualPropertys, deleted, false);
                    start++;
                }
            }
            if (dbVersion > 5)
            { 
                if (CheckStart(dtBail, start) == 22)
                {
                    BuildList(OrgKeytermService.GetChanges(dbContext.Orgkeyterms, currentUser, origin, dtSince, project, startId), Tables.OrgKeyTerms, changes);
                    BuildList(OrgKeytermService.GetDeletedSince(dbContext.Orgkeyterms, currentUser, origin, dtSince), Tables.OrgKeyTerms, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 23)
                {
                    BuildList(OrgKeytermReferenceService.GetChanges(dbContext.Orgkeytermreferences, currentUser, origin, dtSince, project, startId), Tables.OrgKeyTermReferences, changes);
                    BuildList(OrgKeytermReferenceService.GetDeletedSince(dbContext.Orgkeytermreferences, currentUser, origin, dtSince), Tables.OrgKeyTermReferences, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 24)
                {
                    BuildList(OrgKeytermTargetService.GetChanges(dbContext.Orgkeytermtargets, currentUser, origin, dtSince, project, startId), Tables.OrgKeyTermTargets, changes);
                    BuildList(OrgKeytermTargetService.GetDeletedSince(dbContext.Orgkeytermtargets, currentUser, origin, dtSince), Tables.OrgKeyTermTargets, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 25)
                {
                    BuildList(SharedResourceService.GetChanges(dbContext.Sharedresources, currentUser, origin, dtSince, project, startId), Tables.SharedResources, changes);
                    BuildList(SharedResourceService.GetDeletedSince(dbContext.Sharedresources, currentUser, origin, dtSince), Tables.SharedResources, deleted, false);
                    start++;
                }
                if (CheckStart(dtBail, start) == 26)
                {
                    BuildList(SharedResourceReferenceService.GetChanges(dbContext.Sharedresourcereferences, currentUser, origin, dtSince, project, startId), Tables.SharedResourceReferences, changes);
                    BuildList(SharedResourceReferenceService.GetDeletedSince(dbContext.Sharedresourcereferences, currentUser, origin, dtSince), Tables.SharedResourceReferences, deleted, false);
                    start++;
                }
            }
            if (dbVersion > 6)
            {
                if (CheckStart(dtBail, start) == LAST_ADD)
                {
                    BuildList(GraphicService.GetChanges(dbContext.Graphics, currentUser, origin, dtSince, project, startId), Tables.Graphics, changes);
                    BuildList(GraphicService.GetDeletedSince(dbContext.Graphics, currentUser, origin, dtSince), Tables.Graphics, deleted, false);
                    start++;
                }
            }
            DCReturn ret = new (changes, deleted, start == LAST_ADD + 1 ? -1 : start);
            return ret;
        }
    }
}
