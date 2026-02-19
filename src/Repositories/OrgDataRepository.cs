using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using System.Collections.Immutable;

namespace SIL.Transcriber.Repositories
{
    public class OrgDataRepository(
        ITargetedFields targetedFields,
        AppDbContextResolver contextResolver,
        IResourceGraph resourceGraph,
        IResourceFactory resourceFactory,
        IEnumerable<IQueryConstraintProvider> constraintProviders,
        ILoggerFactory loggerFactory,
        IResourceDefinitionAccessor resourceDefinitionAccessor,
        CurrentUserRepository currentUserRepository,
        OrganizationRepository orgRepository,
        GroupMembershipRepository grpMemRepository,
        GroupRepository grpRepository,
        IMetaBuilder metaBuilder,
        IJsonApiOptions options
        ) : BaseRepository<Orgdata>(
            targetedFields,
            contextResolver,
            resourceGraph,
            resourceFactory,
            constraintProviders,
            loggerFactory,
            resourceDefinitionAccessor,
            currentUserRepository
            )
    {
        protected readonly OrganizationRepository organizationRepository = orgRepository;
        protected readonly GroupMembershipRepository gmRepository = grpMemRepository;
        protected readonly GroupRepository groupRepository = grpRepository;
        protected readonly ResourceInfo resourceInfo = new(resourceGraph, options, resourceDefinitionAccessor, metaBuilder);
        protected readonly IResourceFactory _resourceFactory = resourceFactory;
        protected readonly IEnumerable<IQueryConstraintProvider> _constraintProviders = constraintProviders;
        const int NUMTABLES = 32;

        private IQueryable<Orgdata> GetData(
            IQueryable<Orgdata> entities,
            string start,
            int version = 1
        )
        {
            Orgdata orgData = entities.First();
            string data = "";
            if (!int.TryParse(start, out int iStart))
                iStart = 0;

            int iStartNext = iStart;
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            do
            {
                if (iStartNext <= 5)
                {
                    if (!CheckAdd(0,
                            ToJson(dbContext.Activitystates, resourceInfo),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    if (!CheckAdd(1,
                            ToJson(dbContext.Projecttypes, resourceInfo),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    if (!CheckAdd(2,
                            ToJson(dbContext.Plantypes, resourceInfo),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    if (!CheckAdd(3, ToJson(dbContext.Roles, resourceInfo), dtBail, ref iStartNext, ref data))
                        break;
                    if (!CheckAdd(4,
                            ToJson(dbContext.Integrations, resourceInfo),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    if (!CheckAdd(5, ToJson(dbContext.Passagetypes, resourceInfo), dtBail, ref iStartNext, ref data))
                        break;
                }
                IQueryable<Organization> orgs = organizationRepository.GetMine().Where(g => !g.Archived); //this limits to current user

                if (!CheckAddData(6, orgs, dtBail, ref iStartNext, ref data, resourceInfo))
                    break;
                if (iStartNext < 8)
                {
                    IQueryable<Models.Group> groups = groupRepository.GetMine().Where(g => !g.Archived);
                    if (!CheckAddData(7, gmRepository.GetMine(groups).Where(g => !g.Archived), dtBail, ref iStartNext, ref data, resourceInfo))
                        break;
                    //groups
                    if (!CheckAddData(8, groups, dtBail, ref iStartNext, ref data, resourceInfo))
                        break;
                }
                //invitations
                if (!CheckAddData(9,
                        dbContext.InvitationsData.Join(
                                orgs,
                                i => i.OrganizationId,
                                o => o.Id,
                                (i, o) => i

                        ),
                        dtBail,
                        ref iStartNext,
                        ref data, resourceInfo)
                )
                    break;

                if (iStartNext < 12)
                {
                    //orgmems
                    IQueryable<Organizationmembership> oms = dbContext.OrganizationmembershipsData
                    .Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om)
                    .Where(x => !x.Archived);
                    if (!CheckAddData(10, oms, dtBail, ref iStartNext, ref data, resourceInfo))
                        break;
                    int userid = CurrentUser?.Id ?? -1;
                    //users
                    if (!CheckAddData(11,
                                oms.Any()
                                    ? dbContext.Users
                                        .Join(oms, u => u.Id, om => om.UserId, (u, om) => u)
                                        .Where(x => !x.Archived)
                                    : dbContext.Users.Where(x => x.Id == userid),
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo
                        )
                    )
                        break;
                }
                //projects
                IQueryable<Project> projects = dbContext.ProjectsData
                    .Join(orgs, p => p.OrganizationId, o => o.Id, (p, o) => p)
                    .Where(x => !x.Archived);
                if (!CheckAddData(12, projects, dtBail, ref iStartNext, ref data, resourceInfo))
                    break;
                //projectintegrations
                if (!CheckAddData(13,
                            dbContext.ProjectintegrationsData
                                .Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl)
                                .Where(x => !x.Archived),
                        dtBail,
                        ref iStartNext,
                        ref data, resourceInfo)
                )
                    break;
                //plans
                if (!CheckAddData(14,
                            dbContext.PlansData
                                .Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl)
                                .Where(x => !x.Archived),
                            dtBail,
                        ref iStartNext,
                        ref data, resourceInfo)
                )
                    break;

                if (version > 3)
                {
                    if (!CheckAddData(15,
                            dbContext.Workflowsteps.Where(x => !x.Archived),
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo)
                    )
                        break;
                    IEnumerable<int> ids = orgs.Select(o => o.Id);
                    IEnumerable<Artifactcategory> catlist = [.. dbContext.ArtifactcategoriesData
                        .Where(c =>
                            (c.OrganizationId == null || ids.Contains((int)c.OrganizationId))
                            && !c.Archived
                    )];
                    IQueryable<Sharedresource>? sharedres = null;
                    if (version > 5)
                    {
                        IQueryable<Note> notes = dbContext.Notes.Where(x => ids.Contains((int)x.OrganizationId));
                        sharedres = dbContext.SharedresourcesData.Where(x => !x.Archived);
                        if (version > 8)
                        {
                            IQueryable<int?> noteids = notes.Select(x => x.ResourceId);
                            sharedres = sharedres.Where(sr => !sr.Note || noteids.Contains(sr.Id));
                        }
                        List<Artifactcategory> resourcecats = sharedres.Join(dbContext.ArtifactcategoriesData, s => s.ArtifactCategoryId, c => c.Id, (s, c) => c).ToList();
                        catlist = catlist.Union(resourcecats, new RecordEqualityComparer<Artifactcategory>());
                    }
                    Logger.LogInformation("!!! iStartNext {iStartNext}", iStartNext);

                    if (!CheckAdd(16,
                            ToJson(catlist, resourceInfo),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    if (!CheckAddData(17, dbContext.ArtifacttypesData
                        .Include(c => c.Organization)
                        .Where(c =>
                                (c.OrganizationId == null || ids.Contains((int)c.OrganizationId))
                                && !c.Archived
                        ), dtBail, ref iStartNext, ref data, resourceInfo))
                        break;
                    if (!CheckAddData(18,
                                dbContext.OrgworkflowstepsData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived),
                            dtBail,
                            ref iStartNext,
                            ref data, resourceInfo)
                    )
                        break;

                    IQueryable<Intellectualproperty>? ip = dbContext.IntellectualPropertyData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived);
                    if (!CheckAddData(19, ip, dtBail, ref iStartNext, ref data, resourceInfo))
                        break;
                    IQueryable<int?>? sectionTitles = null;
                    IQueryable<Bible>? bibles = null;
                    IQueryable<Graphic>? graphics = null;
                    if (version > 5)
                    {
                        IQueryable<Orgkeyterm>? orgkeyterms = dbContext.OrgKeytermsData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived);
                        if (!CheckAddData(20, orgkeyterms, dtBail, ref iStartNext, ref data, resourceInfo))
                            break;
                        IQueryable<Orgkeytermtarget> orgkttargets = dbContext.OrgKeytermTargetsData
                                        .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                        .Where(x => !x.Archived);
                        if (!CheckAddData(21, orgkttargets, dtBail, ref iStartNext, ref data, resourceInfo))
                            break;
                        //do I need the sections from orgkeytermreferences?
                        if (!CheckAddData(22, dbContext.OrgKeytermReferencesData
                                        .Join(orgkeyterms, c => c.OrgkeytermId, o => o.Id, (c, o) => c)
                                        .Where(x => !x.Archived), dtBail, ref iStartNext, ref data, resourceInfo))
                            break;


                        if (sharedres != null)
                        {
                            IQueryable<Passage> linkedpassages = sharedres.Join(dbContext.PassagesData, sr => sr.PassageId, p => p.Id, (sr, p) => p);
                            IQueryable<Section> linkedsections = linkedpassages.Join(dbContext.SectionsData, p => p.SectionId, s => s.Id, (p, s) => s);
                            if (!CheckAddData(23, linkedsections, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                            if (!CheckAddData(24, linkedpassages, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                            if (!CheckAddData(25, sharedres, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                            sectionTitles = linkedsections.Select(s => s.TitleMediafileId);
                        }
                        else
                            iStartNext += 3;

                        if (!CheckAddData(26, dbContext.SharedresourcereferencesData
                                        .Where(x => !x.Archived), dtBail, ref iStartNext, ref data, resourceInfo))
                            break;
                        if (version > 6)
                        {
                            IQueryable<Bible> allbibles = dbContext.BiblesData.Where(x => !x.Archived);
                            if (!CheckAddData(27, allbibles, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                            //orgbibles
                            IQueryable<Organizationbible> obs = dbContext.OrganizationbiblesData
                                            .Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om)
                                            .Where(x => !x.Archived);
                            if (!CheckAddData(28, obs, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                            bibles = allbibles.Join(obs, b => b.Id, o => o.BibleId, (b, o) => b);
                            graphics = dbContext.GraphicsData.Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                        .Where(x => !x.Archived);
                            if (!CheckAddData(29, graphics, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;

                        }
                        else
                            iStartNext = NUMTABLES;
                        if (version > 9)
                        {
                            IQueryable<Organizationscheme> schemes = dbContext.OrganizationschemesData
                                                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                                                    .Where(x => !x.Archived);
                            if (!CheckAddData(30, schemes, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                            IQueryable<Organizationschemestep> steps = dbContext.OrganizationschemestepsData
                                                                    .Join(schemes, c => c.OrganizationschemeId, o => o.Id, (c, o) => c)
                                                                    .Where(x => !x.Archived);
                            if (!CheckAddData(31, steps, dtBail, ref iStartNext, ref data, resourceInfo))
                                break;
                        }
                        else
                            iStartNext = NUMTABLES;
                    }
                    else
                        iStartNext = NUMTABLES;
                    if (iStartNext == NUMTABLES && DateTime.Now < dtBail)
                    {
                        //Category Title Media
                        IEnumerable<int?> listIds = [.. catlist.Select(c => c.TitleMediafileId).Union(ip.Select(i => i.ReleaseMediafileId))];
                        if (sharedres != null)
                            listIds = listIds.Union(sharedres.Select(sr => sr.TitleMediafileId));
                        if (bibles != null)
                        {
                            listIds = listIds.Union(bibles.Select(b => b.IsoMediafileId)).Union(bibles.Select(b => b.BibleMediafileId));
                        }
                        if (graphics != null)
                            listIds = listIds.Union(graphics.Select(g => g.MediafileId));
                        if (sectionTitles != null)
                            listIds = listIds.Union(sectionTitles);

                        List<Mediafile> linkedmedia = [.. dbContext.MediafilesData.Where(x => !x.Archived && listIds.Contains(x.Id))];

                        if (!CheckAdd(NUMTABLES, ToJson(linkedmedia, resourceInfo), dtBail, ref iStartNext, ref data))
                            break;
                    }
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data " + iStart.ToString());

            orgData.Json = data + FinishData();
            orgData.StartNext = iStartNext;
            return entities;
        }

        protected override IQueryable<Orgdata> GetAll()
        {
            List<Orgdata> entities = [new Orgdata()];
            return entities.AsAsyncQueryable();
        }

        public override IQueryable<Orgdata> FromCurrentUser(IQueryable<Orgdata>? entities = null)
        {
            return GetAll();
        }

        protected override IQueryable<Orgdata> FromStartIndex(
            IQueryable<Orgdata>? entities,
            string startIndex,
            string version = "",
            string projectid = ""
        )
        {
            dynamic? x = JsonConvert.DeserializeObject(version);
            int filterVersion = x?.version ?? 1;

            IQueryable<Orgdata> result = GetData(
                entities ?? GetAll(),
                startIndex,
                filterVersion
            );
            return result;
        }

        public override IQueryable<Orgdata> FromProjectList(
            IQueryable<Orgdata>? entities,
            string idList
        )
        {
            return entities ?? GetAll();
        }
    }
}
