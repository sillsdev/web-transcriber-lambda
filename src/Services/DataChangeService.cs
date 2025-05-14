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

    public class DataChangeService(
        IResourceRepositoryAccessor repositoryAccessor,
        IQueryLayerComposer queryLayerComposer,
        IPaginationContext paginationContext,
        IJsonApiOptions options,
        ILoggerFactory loggerFactory,
        IJsonApiRequest request,
        IResourceChangeTracker<Datachanges> resourceChangeTracker,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        DatachangesRepository repository,
        ArtifactCategoryService artifactCategoryService,
        ArtifactTypeService artifactTypeService,
        BibleService bibleService,
        CommentService commentService,
        DiscussionService discussionService,
        GraphicService graphicService,
        GroupMembershipService gmService,
        GroupService groupService,
        IntellectualPropertyService intellectualPropertyService,
        InvitationService invitationService,
        MediafileService mediafileService,
        OrganizationBibleService orgBibleService,
        OrganizationMembershipService omService,
        OrganizationSchemeService schemeService,
        OrganizationSchemeStepService schemestepService,
        OrganizationService organizationService,
        OrgWorkflowStepService orgWorkflowStepService,
        OrgKeytermService orgKeytermService,
        OrgKeytermReferenceService orgKeytermReferenceService,
        OrgKeytermTargetService orgKeytermTargetService,
        PassageService passageService,
        PassageStateChangeService passageStateChangeService,
        PassagetypeService passagetypeService,
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
        ) : BaseService<Datachanges>(
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
        private readonly ArtifactCategoryService ArtifactCategoryService = artifactCategoryService;
        private readonly ArtifactTypeService ArtifactTypeService = artifactTypeService;
        private readonly BibleService BibleService = bibleService;
        private readonly CommentService CommentService = commentService;
        private readonly DiscussionService DiscussionService = discussionService;
        private readonly GraphicService GraphicService = graphicService;
        private readonly GroupMembershipService GMService = gmService;
        private readonly GroupService GroupService = groupService;
        private readonly IntellectualPropertyService IntellectualPropertyService = intellectualPropertyService;
        private readonly InvitationService InvitationService = invitationService;
        private readonly MediafileService MediafileService = mediafileService;
        private readonly PassagetypeService PassagetypeService = passagetypeService;
        private readonly OrganizationBibleService OrgBibleService = orgBibleService;
        private readonly OrganizationMembershipService OrgMemService = omService;
        private readonly OrganizationSchemeService SchemeService = schemeService;
        private readonly OrganizationSchemeStepService SchemeStepService = schemestepService;
        private readonly OrganizationService OrganizationService = organizationService;
        private readonly OrgKeytermService OrgKeytermService = orgKeytermService;
        private readonly OrgKeytermReferenceService OrgKeytermReferenceService = orgKeytermReferenceService;
        private readonly OrgKeytermTargetService OrgKeytermTargetService = orgKeytermTargetService;
        private readonly OrgWorkflowStepService OrgWorkflowStepService = orgWorkflowStepService;
        private readonly PassageService PassageService = passageService;
        private readonly PassageStateChangeService PassageStateChangeService = passageStateChangeService;
        private readonly PlanService PlanService = planService;
        private readonly ProjectIntegrationService ProjIntService = piService;
        private readonly ProjectService ProjectService = projectService;
        private readonly SectionResourceService SectionResourceService = sectionResourceService;
        private readonly SectionResourceUserService SectionResourceUserService = sectionResourceUserService;
        private readonly SectionService SectionService = sectionService;
        private readonly SharedResourceService SharedResourceService = sharedResourceService;
        private readonly SharedResourceReferenceService SharedResourceReferenceService = sharedResourceReferenceService;
        private readonly UserService UserService = userService;
        private readonly WorkflowStepService WorkflowStepService = workflowStepService;
        private readonly CurrentUserRepository CurrentUserRepository = currentUserRepository;
        private readonly List<OrbitId> changes = [];
        private readonly List<OrbitId> deleted = [];
        protected readonly AppDbContext dbContext = (AppDbContext)contextResolver.GetContext();

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
            foreach (BaseModel m in recs)
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
        public Datachanges GetProjectTableChanges(
                string table,
                DateTime dtSince, int project, int startId
            )
        {
            DateTime dtNow = DateTime.UtcNow;
            startId = GetTableChanges(table, "any", dtSince, 0, project, startId);
            _ = changes.RemoveAll(c => c.Ids.Count == 0);
            _ = deleted.RemoveAll(d => d.Ids.Count == 0);
            return new Datachanges()
            {
                Id = 1,
                Startnext = startId,
                Querydate = dtNow,
                Changes = [.. changes],
                Deleted = [.. deleted]
            };
        }

        public Datachanges GetProjectChanges(
            string origin,
            ProjDate?[] projects,
            string version = "1",
            int start = 0
        )
        {
            DateTime dtNow = DateTime.UtcNow;
            if (!int.TryParse(version, out int dbVersion))
                dbVersion = 1;
            List<OrbitId> changes = [];
            List<OrbitId> deleted = [];
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
                Changes = [.. changes],
                Deleted = [.. deleted]
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
                    Changes = [.. changes],
                    Deleted = [.. deleted]
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

        public class DCReturn(List<OrbitId> Changes, List<OrbitId> Deleted, int StartNext)
        {
            public int startNext = StartNext;
            public List<OrbitId> changes = Changes;
            public List<OrbitId> deleted = Deleted;
        }

        private static int CheckStart(DateTime dtBail, int completed)
        {
            //Logger.LogCritical($"{check} : {DateTime.Now} {dtBail}");
            return DateTime.Now > dtBail ? 1000 : completed;
        }

        private int GetTableChanges(string table,
            string origin,
            DateTime dtSince,
            int currentUser,
            int project,
            int startId)
        {
            switch (table)
            {
                case "bible":
                    startId = BuildList(BibleService.GetChanges(dbContext.Bibles, currentUser, origin, dtSince, project, startId), Tables.Bibles, changes);
                    BuildList(BibleService.GetDeletedSince(dbContext.Bibles, currentUser, origin, dtSince, project, 0), Tables.Bibles, deleted, false);
                    break;
                case "graphic":
                    startId = BuildList(GraphicService.GetChanges(dbContext.Graphics, currentUser, origin, dtSince, project, startId), Tables.Graphics, changes);
                    BuildList(GraphicService.GetDeletedSince(dbContext.Graphics, currentUser, origin, dtSince, project, 0), Tables.Graphics, deleted, false);
                    break;
                case "group":
                    startId = BuildList(GroupService.GetChanges(dbContext.Groups, currentUser, origin, dtSince, project, startId), Tables.Groups, changes);
                    BuildList(GroupService.GetDeletedSince(dbContext.Groups, currentUser, origin, dtSince, project, 0), Tables.Groups, deleted, false);
                    break;
                case "groupmembership":
                    startId = BuildList(GMService.GetChanges(dbContext.Groupmemberships, currentUser, origin, dtSince, project, startId), Tables.GroupMemberships, changes);
                    BuildList(GMService.GetDeletedSince(dbContext.Groupmemberships, currentUser, origin, dtSince, project, 0), Tables.GroupMemberships, deleted, false);
                    break;
                case "invitation":
                    startId = BuildList(InvitationService.GetChanges(dbContext.Invitations, currentUser, origin, dtSince, project, startId), Tables.Invitations, changes);
                    break;
                case "mediafile":
                    startId = BuildList(MediafileService.GetChanges(dbContext.Mediafiles, currentUser, origin, dtSince, project, startId), Tables.Mediafiles, changes);
                    if (startId == -1)
                        BuildList(MediafileService.GetDeletedSince(dbContext.Mediafiles, currentUser, origin, dtSince, project, 0), Tables.Mediafiles, deleted, false);
                    break;
                case "organization":
                    startId = BuildList(OrganizationService.GetChanges(dbContext.Organizations, currentUser, origin, dtSince, project, startId), Tables.Organizations, changes);
                    BuildList(OrganizationService.GetDeletedSince(dbContext.Organizations, currentUser, origin, dtSince, project, 0), Tables.Organizations, deleted, false);
                    break;
                case "organizationbible":
                    startId = BuildList(OrgBibleService.GetChanges(dbContext.Organizationbibles, currentUser, origin, dtSince, project, startId), Tables.OrganizationBibles, changes);
                    BuildList(OrgBibleService.GetDeletedSince(dbContext.Organizationbibles, currentUser, origin, dtSince, project, 0), Tables.OrganizationBibles, deleted, false);
                    break;
                case "organizationmembership":
                    startId = BuildList(OrgMemService.GetChanges(dbContext.Organizationmemberships, currentUser, origin, dtSince, project, startId), Tables.OrganizationMemberships, changes);
                    BuildList(OrgMemService.GetDeletedSince(dbContext.Organizationmemberships, currentUser, origin, dtSince, project, 0), Tables.OrganizationMemberships, deleted, false);
                    break;
                case "organizationscheme":
                    startId = BuildList(SchemeService.GetChanges(dbContext.Organizationschemes, currentUser, origin, dtSince, project, startId), Tables.OrganizationSchemes, changes);
                    BuildList(SchemeService.GetDeletedSince(dbContext.Organizationschemes, currentUser, origin, dtSince, project, 0), Tables.OrganizationSchemes, deleted, false);
                    break;
                case "organizationschemestep":
                    startId = BuildList(SchemeStepService.GetChanges(dbContext.Organizationschemesteps, currentUser, origin, dtSince, project, startId), Tables.OrganizationSchemeSteps, changes);
                    BuildList(SchemeStepService.GetDeletedSince(dbContext.Organizationschemesteps, currentUser, origin, dtSince, project, 0), Tables.OrganizationSchemeSteps, deleted, false);
                    break;
                case "passage":
                    startId = BuildList(PassageService.GetChanges(dbContext.Passages, currentUser, origin, dtSince, project, startId), Tables.Passages, changes);
                    BuildList(PassageService.GetDeletedSince(dbContext.Passages, currentUser, origin, dtSince, project, 0), Tables.Passages, deleted, false);
                    break;
                case "passagestatechange":
                    startId = BuildList(PassageStateChangeService.GetChanges(dbContext.Passagestatechanges, currentUser, origin, dtSince, project, startId), Tables.PassageStateChanges, changes);
                    break;
                case "plan":
                    startId = BuildList(PlanService.GetChanges(dbContext.Plans, currentUser, origin, dtSince, project, startId), Tables.Plans, changes);
                    BuildList(PlanService.GetDeletedSince(dbContext.Plans, currentUser, origin, dtSince, project, 0), Tables.Plans, deleted, false);
                    break;
                case "project":
                    startId = BuildList(ProjectService.GetChanges(dbContext.Projects, currentUser, origin, dtSince, project, startId), Tables.Projects, changes);
                    BuildList(ProjectService.GetDeletedSince(dbContext.Projects, currentUser, origin, dtSince, project, 0), Tables.Projects, deleted, false);
                    break;
                case "projectintegration":
                    startId = BuildList(ProjIntService.GetChanges(dbContext.Projectintegrations, currentUser, origin, dtSince, project, startId), Tables.ProjectIntegrations, changes);
                    break;
                case "section":
                    startId = BuildList(SectionService.GetChanges(dbContext.Sections, currentUser, origin, dtSince, project, startId), Tables.Sections, changes);
                    BuildList(SectionService.GetDeletedSince(dbContext.Sections, currentUser, origin, dtSince, project, 0), Tables.Sections, deleted, false);
                    break;
                case "user":
                    startId = BuildList(UserService.GetChanges(dbContext.Users, currentUser, origin, dtSince, project, startId), Tables.Users, changes);
                    BuildList(UserService.GetDeletedSince(dbContext.Users, currentUser, origin, dtSince, project, 0), Tables.Users, deleted, false);
                    break;
                case "artifactcategory":
                    startId = BuildList(ArtifactCategoryService.GetChanges(dbContext.Artifactcategorys, currentUser, origin, dtSince, project, startId), Tables.ArtifactCategorys, changes);
                    BuildList(ArtifactCategoryService.GetDeletedSince(dbContext.Artifactcategorys, currentUser, origin, dtSince, project, 0), Tables.ArtifactCategorys, deleted, false);
                    break;
                case "artifacttype":
                    startId = BuildList(ArtifactTypeService.GetChanges(dbContext.Artifacttypes, currentUser, origin, dtSince, project, startId), Tables.ArtifactTypes, changes);
                    BuildList(ArtifactTypeService.GetDeletedSince(dbContext.Artifacttypes, currentUser, origin, dtSince, project, 0), Tables.ArtifactTypes, deleted, false);
                    break;
                case "discussion":
                    startId = BuildList(DiscussionService.GetChanges(dbContext.Discussions, currentUser, origin, dtSince, project, startId), Tables.Discussions, changes);
                    BuildList(DiscussionService.GetDeletedSince(dbContext.Discussions, currentUser, origin, dtSince, project, 0), Tables.Discussions, deleted, false);
                    break;
                case "comment":
                    startId = BuildList(CommentService.GetChanges(dbContext.Comments, currentUser, origin, dtSince, project, startId), Tables.Comments, changes);
                    BuildList(CommentService.GetDeletedSince(dbContext.Comments, currentUser, origin, dtSince, project, 0), Tables.Comments, deleted, false);
                    break;
                case "passagetype":
                    startId = BuildList(PassagetypeService.GetChanges(dbContext.Passagetypes, currentUser, origin, dtSince, project, startId), Tables.PassageTypes, changes);
                    break;
                case "orgworkflowstep":
                    startId = BuildList(OrgWorkflowStepService.GetChanges(dbContext.Orgworkflowsteps, currentUser, origin, dtSince, project, startId), Tables.OrgWorkflowSteps, changes);
                    BuildList(OrgWorkflowStepService.GetDeletedSince(dbContext.Orgworkflowsteps, currentUser, origin, dtSince, project, 0), Tables.OrgWorkflowSteps, deleted, false);
                    break;
                case "sectionresource":
                    startId = BuildList(SectionResourceService.GetChanges(dbContext.Sectionresources, currentUser, origin, dtSince, project, startId), Tables.SectionResources, changes);
                    BuildList(SectionResourceService.GetDeletedSince(dbContext.Sectionresources, currentUser, origin, dtSince, project, 0), Tables.SectionResources, deleted, false);
                    break;
                case "sectionresourceuser":
                    startId = BuildList(SectionResourceUserService.GetChanges(dbContext.Sectionresourceusers, currentUser, origin, dtSince, project, startId), Tables.SectionResourceUsers, changes);
                    BuildList(SectionResourceUserService.GetDeletedSince(dbContext.Sectionresourceusers, currentUser, origin, dtSince, project, 0), Tables.SectionResourceUsers, deleted, false);
                    break;
                case "workflowstep":
                    startId = BuildList(WorkflowStepService.GetChanges(dbContext.Workflowsteps, currentUser, origin, dtSince, project, startId), Tables.WorkflowSteps, changes);
                    BuildList(WorkflowStepService.GetDeletedSince(dbContext.Workflowsteps, currentUser, origin, dtSince, project, 0), Tables.WorkflowSteps, deleted, false);
                    break;
                case "intellectualproperty":
                    startId = BuildList(IntellectualPropertyService.GetChanges(dbContext.IntellectualPropertys, currentUser, origin, dtSince, project, startId), Tables.IntellectualPropertys, changes);
                    BuildList(IntellectualPropertyService.GetDeletedSince(dbContext.IntellectualPropertys, currentUser, origin, dtSince, project, 0), Tables.IntellectualPropertys, deleted, false);
                    break;
                case "orgkeyterm":
                    startId = BuildList(OrgKeytermService.GetChanges(dbContext.Orgkeyterms, currentUser, origin, dtSince, project, startId), Tables.OrgKeyTerms, changes);
                    BuildList(OrgKeytermService.GetDeletedSince(dbContext.Orgkeyterms, currentUser, origin, dtSince, project, 0), Tables.OrgKeyTerms, deleted, false);
                    break;
                case "orgkeytermreference":
                    startId = BuildList(OrgKeytermReferenceService.GetChanges(dbContext.Orgkeytermreferences, currentUser, origin, dtSince, project, startId), Tables.OrgKeyTermReferences, changes);
                    BuildList(OrgKeytermReferenceService.GetDeletedSince(dbContext.Orgkeytermreferences, currentUser, origin, dtSince, project, 0), Tables.OrgKeyTermReferences, deleted, false);
                    break;
                case "orgkeytermtarget":
                    startId = BuildList(OrgKeytermTargetService.GetChanges(dbContext.Orgkeytermtargets, currentUser, origin, dtSince, project, startId), Tables.OrgKeyTermTargets, changes);
                    BuildList(OrgKeytermTargetService.GetDeletedSince(dbContext.Orgkeytermtargets, currentUser, origin, dtSince, project, 0), Tables.OrgKeyTermTargets, deleted, false);
                    break;
                case "sharedresource":
                    startId = BuildList(SharedResourceService.GetChanges(dbContext.Sharedresources, currentUser, origin, dtSince, project, startId), Tables.SharedResources, changes);
                    BuildList(SharedResourceService.GetDeletedSince(dbContext.Sharedresources, currentUser, origin, dtSince, project, 0), Tables.SharedResources, deleted, false);
                    break;
                case "sharedresourcereference":
                    startId = BuildList(SharedResourceReferenceService.GetChanges(dbContext.Sharedresourcereferences, currentUser, origin, dtSince, project, startId), Tables.SharedResourceReferences, changes);
                    BuildList(SharedResourceReferenceService.GetDeletedSince(dbContext.Sharedresourcereferences, currentUser, origin, dtSince, project, 0), Tables.SharedResourceReferences, deleted, false);
                    break;
            }
            return startId;

        }
        DCReturn GetChanges(
            string origin,
            DateTime dtSince,
            int currentUser,
            int project,
            int dbVersion,
            int start
        )
        {
            string[] tables = [ "user", "organization", "organizationmembership",
                "group", "groupmembership", "project", "plan", "section", "passage", "passagestatechange",
                "projectintegration", "invitation", "mediafile"];
            if (dbVersion > 3)
                tables =
                [
                    .. tables,
                    .. new string[] { "artifactcategory", "artifactttype",
                        "discussion", "comment", "orgworkflowstep","sectionresource", "sectionresourceuser",
                    "workflowstep", "intellectualproperty"},
                ];
            if (dbVersion > 5)
                tables =
                [
                    .. tables,
                    .. new string[] { "orgkeyterm", "orgkeytermreference", "orgkeytermtarget",
                        "sharedresource", "sharedresourcereference" },
                ];
            if (dbVersion > 6)
                tables = [.. tables, .. new string[] { "graphic" }];
            if (dbVersion > 7)
                tables = [.. tables, .. new string[] { "bible", "organizationbible" }];
            if (dbVersion > 9)
                tables = [.. tables, .. new string[] { "organizationscheme", "organizationschemestep" }];
            //Logger.LogCritical("GetChanges {start} {dtSince} {project}", start, dtSince, project);
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            int startId = -1;
            start = StartIndex.GetStart(start, ref startId);

            int checkstart = CheckStart(dtBail, start);
            while (checkstart < tables.Length)
            {
                startId = GetTableChanges(tables[checkstart], origin, dtSince, currentUser, project, startId);
                if (startId == -1)
                    start++;
                else
                {
                    start = StartIndex.SetStart(start, ref startId);
                    break;
                }
                checkstart = CheckStart(dtBail, start);
            }
            DCReturn ret = new (changes, deleted, start == tables.Length ? -1 : start);
            return ret;
        }
    }
}
