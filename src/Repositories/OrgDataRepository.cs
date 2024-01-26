using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Response;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serialization;
using SIL.Transcriber.Utility;
using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace SIL.Transcriber.Repositories
{
    public class OrgDataRepository : BaseRepository<Orgdata>
    {
        protected readonly OrganizationRepository organizationRepository;
        protected readonly GroupMembershipRepository gmRepository;
        protected readonly GroupRepository groupRepository;
        protected readonly IResourceGraph _resourceGraph;
        protected readonly IResourceFactory _resourceFactory;
        protected readonly IEnumerable<IQueryConstraintProvider> _constraintProviders;
        protected readonly IJsonApiOptions _options;
        protected readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;
        protected readonly IMetaBuilder _metaBuilder;

        public OrgDataRepository(
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
        )
            : base(
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
            organizationRepository = orgRepository;
            gmRepository = grpMemRepository;
            groupRepository = grpRepository;
            _resourceGraph = resourceGraph;
            _resourceFactory = resourceFactory;
            _constraintProviders = constraintProviders;
            _options = options;
            _resourceDefinitionAccessor = resourceDefinitionAccessor;
            _metaBuilder = metaBuilder;
        }

        private string ToJson<TResource>(IEnumerable<TResource> resources)
             where TResource : class, IIdentifiable
        {
            string? withIncludes =
            SerializerHelpers.ResourceListToJson(
                resources,
                _resourceGraph,
                _options,
                _resourceDefinitionAccessor,
                _metaBuilder
            );
            if (withIncludes.Contains("included"))
            {
                dynamic tmp = JObject.Parse(withIncludes);
                tmp.Remove("included");
                withIncludes = tmp.ToString();
            }
            Regex rgxnewlines = new("\r\n|\n");
            Regex rgxmultiplspaces = new("\t|\\s+");
            return rgxmultiplspaces.Replace(rgxnewlines.Replace(withIncludes, ""), " ");
        }

        public bool CheckAddGraphics(
        int check,
        IQueryable<Graphic> list,
        DateTime dtBail,
        ref int start,
        ref string data
        )
        {
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return false;
            int startId = -1;
            int starttable = StartIndex.GetStart(start, ref startId);
            int lastId = -1;
            if (starttable == check)
            {
                List<Graphic>? lst = startId > 0 ? list.Where(m => m.Id >= startId).ToList() : list.ToList();
                string thisData = ToJson(lst);

                while (thisData.Length > (1000000 * 4))
                {
                    int cnt = lst.Count;
                    Graphic mid = lst[cnt/2];
                    lastId = mid.Id;
                    lst = list.Where(m => m.Id >= startId && m.Id < lastId).ToList();
                    thisData = ToJson(lst);
                }
                if (data.Length + thisData.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisData;
                start = StartIndex.SetStart(starttable, ref lastId);
                return lastId == 0;
            }

            return true;
        }
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
                if (!CheckAdd(0,
                        ToJson(dbContext.Activitystates),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;
                if (!CheckAdd(1,
                        ToJson(dbContext.Projecttypes),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;
                if (!CheckAdd(2,
                        ToJson(dbContext.Plantypes),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;
                if (!CheckAdd(3, ToJson(dbContext.Roles), dtBail, ref iStartNext, ref data))
                    break;
                if (!CheckAdd(4,
                        ToJson(dbContext.Integrations),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;
                if (!CheckAdd(5, ToJson(dbContext.Passagetypes),dtBail,ref iStartNext,ref data))
                    break;
                IQueryable<Organization> orgs = organizationRepository.GetMine(); //this limits to current user

                if (!CheckAdd(6, ToJson(orgs), dtBail, ref iStartNext, ref data))
                    break;
                IQueryable<Models.Group> groups = groupRepository.GetMine();
                IQueryable<Groupmembership> gms = gmRepository.GetMine(groups);
                if (!CheckAdd(7, ToJson(gms), dtBail, ref iStartNext, ref data))
                    break;
                //groups
                if (!CheckAdd(8, ToJson(groups), dtBail, ref iStartNext, ref data))
                    break;
                //invitations
                if (!CheckAdd(9,
                        ToJson(dbContext.InvitationsData.Join(
                                orgs,
                                i => i.OrganizationId,
                                o => o.Id,
                                (i, o) => i
                            )
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;


                //orgmems
                IQueryable<Organizationmembership> oms = dbContext.OrganizationmembershipsData
                    .Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om)
                    .Where(x => !x.Archived);
                if (!CheckAdd(10, ToJson(oms), dtBail, ref iStartNext, ref data))
                    break;
                int userid = CurrentUser?.Id ?? -1;
                //users
                if (!CheckAdd(11,
                        ToJson(
                            oms.Any()
                                ? dbContext.Users
                                    .Join(oms, u => u.Id, om => om.UserId, (u, om) => u)
                                    .Where(x => !x.Archived)
                                : dbContext.Users.Where(x => x.Id == userid)
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data
                    )
                )
                    break;

                //projects
                IEnumerable<Project> projects = dbContext.ProjectsData
                    .Join(orgs, p => p.OrganizationId, o => o.Id, (p, o) => p)
                    .Where(x => !x.Archived);
                if (!CheckAdd(12, ToJson(projects), dtBail, ref iStartNext, ref data))
                    break;
                //projectintegrations
                if (!CheckAdd(13,
                        ToJson(
                            dbContext.ProjectintegrationsData
                                .Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl)
                                .Where(x => !x.Archived)
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;
                //plans
                if (!CheckAdd(14,
                        ToJson(
                            dbContext.PlansData
                                .Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl)
                                .Where(x => !x.Archived)
                        ),
                        dtBail,
                        ref iStartNext,
                        ref data)
                )
                    break;

                if (version > 3)
                {
                    if (!CheckAdd(15,
                            ToJson(dbContext.Workflowsteps.Where(x => !x.Archived)),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    IEnumerable<int> ids = orgs.Select(o => o.Id);
                    IQueryable<Artifactcategory> cats = dbContext.ArtifactcategoriesData
                        .Where(c =>
                            (c.OrganizationId == null || ids.Contains((int)c.OrganizationId))
                            && !c.Archived
                    );
                    IQueryable<Sharedresource>? sharedres = null;
                    if (version > 5)
                    {
                        sharedres = dbContext.SharedresourcesData.Where(x => !x.Archived);
                        cats.Union(sharedres.Join(dbContext.ArtifactcategoriesData, s => s.ArtifactCategoryId, c => c.Id, (s, c) => c), new RecordEqualityComparer<Artifactcategory>());
                    }
                    List<Mediafile> linkedmedia = dbContext.MediafilesData.Where(x => !x.Archived).Join(cats, m => m.Id, c => c.TitleMediafileId, (m, c) => m).ToList();

                    if (!CheckAdd(16,
                            ToJson(cats),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;
                    IQueryable<Artifacttype> typs = dbContext.ArtifacttypesData
                        .Include(c => c.Organization)
                        .Where(c =>
                                (c.OrganizationId == null || ids.Contains((int)c.OrganizationId))
                                && !c.Archived
                        );
                    if (!CheckAdd(17, ToJson(typs), dtBail, ref iStartNext, ref data))
                        break;
                    if (!CheckAdd(18,
                            ToJson(
                                dbContext.OrgworkflowstepsData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived)
                            ),
                            dtBail,
                            ref iStartNext,
                            ref data)
                    )
                        break;

                    IQueryable<Intellectualproperty>? ip = dbContext.IntellectualPropertyData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived);
                    if (!CheckAdd(19, ToJson(ip),dtBail,ref iStartNext, ref data ))
                        break;
                    linkedmedia.AddRange(ip.Join(dbContext.MediafilesData, ip => ip.ReleaseMediafileId, m => m.Id, (ip, m) => m));
                    if (version > 5)
                    {
                        IQueryable<Orgkeyterm>? orgkeyterms = dbContext.OrgKeytermsData
                                    .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                    .Where(x => !x.Archived);
                        if (!CheckAdd(20, ToJson(orgkeyterms), dtBail, ref iStartNext, ref data))
                            break;
                        IQueryable<Orgkeytermtarget> orgkttargets = dbContext.OrgKeytermTargetsData
                                        .Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                        .Where(x => !x.Archived);
                        if (!CheckAdd(21, ToJson(orgkttargets), dtBail, ref iStartNext, ref data))
                            break;
                        //do I need the sections from orgkeytermreferences?
                        if (!CheckAdd(22, ToJson(dbContext.OrgKeytermReferencesData
                                        .Join(orgkeyterms, c => c.OrgkeytermId, o => o.Id, (c, o) => c)
                                        .Where(x => !x.Archived)), dtBail, ref iStartNext, ref data))
                            break;

                        if (sharedres != null)
                        {
                            linkedmedia.AddRange(sharedres.Join(dbContext.MediafilesData, sr => sr.TitleMediafileId, m => m.Id, (sr, m) => m));
                            if (!CheckAdd(23, ToJson(sharedres), dtBail, ref iStartNext, ref data))
                                break;
                            IQueryable<Passage> linkedpassages = sharedres.Join(dbContext.PassagesData, sr => sr.PassageId, p => p.Id, (sr, p) => p);
                            if (!CheckAdd(24, ToJson(linkedpassages), dtBail, ref iStartNext, ref data))
                                break;
                            IQueryable<Section> linkedsections = linkedpassages.Join(dbContext.SectionsData, p => p.SectionId, s => s.Id, (p, s) => s);
                            if (!CheckAdd(25, ToJson(linkedsections), dtBail, ref iStartNext, ref data))
                                break;

                            linkedmedia.AddRange(linkedsections.Join(dbContext.MediafilesData, s => s.TitleMediafileId, m => m.Id, (s, m) => m));

                        }
                        else
                            iStartNext+=3;
                        if (!CheckAdd(26, ToJson(dbContext.SharedresourcereferencesData
                                        .Where(x => !x.Archived)), dtBail, ref iStartNext, ref data))
                            break;
                        if (version > 6)
                        {
                            IQueryable<Bible> allbibles = dbContext.BiblesData.Where(x => !x.Archived);
                            if (!CheckAdd(27, ToJson(allbibles), dtBail, ref iStartNext, ref data))
                                break;
                            //orgbibles
                            IQueryable<Organizationbible> obs = dbContext.OrganizationbiblesData
                                            .Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om)
                                            .Where(x => !x.Archived);
                            if (!CheckAdd(28, ToJson(obs), dtBail, ref iStartNext, ref data))
                                break;
                            IQueryable<Bible> bibles = allbibles.Join(obs, b => b.Id, o => o.BibleId, (b, o) => b);
                            linkedmedia.AddRange(dbContext.MediafilesData.Where(x => !x.Archived)
                                                     .Join(bibles, m => m.Id, o => o.IsoMediafileId, (m, o) => m));
                            linkedmedia.AddRange(dbContext.MediafilesData.Where(x => !x.Archived)
                                                    .Join(bibles, m => m.Id, o => o.BibleMediafileId, (m, o) => m));
                            IQueryable<Graphic> graphics = dbContext.GraphicsData.Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c)
                                        .Where(x => !x.Archived);
                            if (!CheckAddGraphics(29, graphics, dtBail, ref iStartNext, ref data))
                                break;
                            linkedmedia.AddRange(graphics.Join(dbContext.MediafilesData, g => g.MediafileId, m => m.Id, (g, m) => m));
                        }
                        else
                            iStartNext = 30;

                    }
                    else
                        iStartNext = 30; 
                    if (!CheckAdd(30, ToJson(linkedmedia), dtBail, ref iStartNext, ref data))
                        break;
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
            List<Orgdata> entities = new() { new Orgdata() };
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
