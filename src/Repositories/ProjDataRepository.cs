using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Serialization;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class ProjDataRepository : BaseRepository<Projdata>
    {
        protected readonly SectionService SectionService;
        protected readonly SectionRepository SectionRepository;
        protected readonly MediafileService MediaService;
        protected readonly IJsonApiOptions _options;
        protected readonly IResourceDefinitionAccessor _resourceDefinitionAccessor;
        protected readonly IMetaBuilder _metaBuilder;
        protected readonly IResourceGraph _resourceGraph;

        public ProjDataRepository(
            ITargetedFields targetedFields,
            AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph,
            IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SectionService sectionService,
            SectionRepository sectionRepository,
            MediafileService mediaService,
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
            SectionService = sectionService;
            SectionRepository = sectionRepository;
            MediaService = mediaService;
            _options = options;
            _resourceDefinitionAccessor = resourceDefinitionAccessor;
            _metaBuilder = metaBuilder;
            _resourceGraph = resourceGraph;
        }
        protected bool CheckAddMedia(
            int check,
            IQueryable<Mediafile> media,
            DateTime dtBail,
            ref int start,
            ref string data
        )
        {
            int divisor = 1000000;
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return false;

            int lastId = start == check ? 0 : start % (check * divisor);
            if (start == check || start / divisor == check)
            {
                int startId = lastId;
                List<Mediafile>? lst = lastId > 0 ? media.Where(m => m.Id > lastId).ToList() : media.ToList();
                string thisData = ToJson(lst);
                lastId = 0;
                while (thisData.Length > (1000000 * 4))
                {
                    int cnt = lst.Count;
                    Mediafile mid = lst[cnt/2];
                    lastId = mid.Id;
                    lst = media.Where(m => m.Id > startId && m.Id <= lastId).ToList();
                    thisData = ToJson(lst);
                }
                if (data.Length + thisData.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisData;
                start = lastId > 0 ? check * divisor + lastId : check+1;
            }
            return true;
        }
        protected bool CheckAddPSC(
            int check,
            IQueryable<Passagestatechange> media,
            DateTime dtBail,
            ref int start,
            ref string data
        )
        {
            int divisor = 1000000;
            //Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail)
                return false;

            int lastId = start == check ? 0 : start % (check * divisor);
            if (start == check || start / divisor == check)
            {
                int startId = lastId;
                List<Passagestatechange>? lst = lastId > 0 ? media.Where(m => m.Id > lastId).ToList() : media.ToList();
                string thisData = ToJson(lst);
                lastId = 0;
                while (thisData.Length > (1000000 * 4))
                {
                    int cnt = lst.Count;
                    Passagestatechange mid = lst[cnt/2];
                    lastId = mid.Id;
                    lst = media.Where(m => m.Id > startId && m.Id <= lastId).ToList();
                    thisData = ToJson(lst);
                }
                if (data.Length + thisData.Length > (1000000 * 4))
                    return false;
                data += (data.Length > 0 ? "," : InitData()) + thisData;
                start = lastId > 0 ? check * divisor + lastId : check + 1;
            }
            return true;
        }

        private string ToJson<TResource>(IEnumerable<TResource> resources)
            where TResource : class, IIdentifiable
        {
            string? withIncludes = 
            SerializerHelpers.ResourceListToJson<TResource>(
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
                return tmp.ToString(); //will this take it out of transcriptions also?? .Replace("\n", "").Replace("\r", "");
            }
            return  withIncludes; //will this take it out of transcriptions also?? .Replace("\n", "").Replace("\r", "");.Replace("\n", "").Replace("\r", "");
        }

        private IQueryable<Projdata> GetData(
            IQueryable<Projdata> entities,
            string project,
            string start,
            int version = 1
        )
        {
            string data = "";
            if (!int.TryParse(start, out int iStart))
                iStart = 0;
            int iStartNext = iStart;
            if (!int.TryParse(project, out int projectid))
                projectid = 0;
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);
            string snapshotDate = DateTime.UtcNow.ToString();

            do
            {
                //plans
                IEnumerable<Plan> plans = dbContext.PlansData.Where(
                    x => x.ProjectId == projectid && !x.Archived
                );
                //plans moved to orgdata
                //if (!CheckAdd(0, plans, dtBail, Serializer, ref iStartNext, ref data)) break;

                //sections
                IQueryable<Section> sections = dbContext.SectionsData
                    .Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s)
                    .Where(x => !x.Archived);
                if (!CheckAdd(0, ToJson<Section>(sections), dtBail, ref iStartNext, ref data))
                    break;

                //passages
                IQueryable<Passage> passages = dbContext.PassagesData
                    .Join(sections, p => p.SectionId, s => s.Id, (p, s) => p)
                    .Where(x => !x.Archived);
                if (!CheckAdd(1, ToJson<Passage>(passages), dtBail, ref iStartNext, ref data))
                    break;

                //mediafiles
                IQueryable<Mediafile>? mediafiles = dbContext.MediafilesData
                    .Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m)
                    .Where(x => !x.Archived).OrderBy(m=>m.Id);
                if (!CheckAddMedia(2, mediafiles, dtBail, ref iStartNext, ref data))
                    break;


                //passagestatechanges
                if (!CheckAddPSC(3, dbContext.PassagestatechangesData.Join(
                                passages,
                                psc => psc.PassageId,
                                p => p.Id,
                                (psc, p) => psc
                            ).OrderBy(m => m.Id), dtBail, ref iStartNext, ref data))
                        break;

                if (iStartNext > 100)
                    break;
                if (version > 3)
                {
                    //discussions
                    IQueryable<Discussion> discussions = dbContext.DiscussionsData
                        .Join(mediafiles, d => d.MediafileId, m => m.Id, (d, m) => d)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(4,
                            ToJson(discussions),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;

                    //comments
                    if (
                        !CheckAdd(5,
                            ToJson<Comment>(
                                dbContext.CommentsData
                                    .Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c)
                                    .Where(x => !x.Archived)
                            ),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;

                    IQueryable<Sectionresource> sectionresources = dbContext.SectionresourcesData
                        .Join(sections, sr => sr.SectionId, s => s.Id, (sr, s) => sr)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(6,
                            ToJson<Sectionresource>(sectionresources),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;

                    IQueryable<Sectionresourceuser> srusers = dbContext.SectionresourceusersData
                        .Join(sectionresources, u => u.SectionResourceId, sr => sr.Id, (u, sr) => u)
                        .Where(x => !x.Archived);
                    if (
                        !CheckAdd(7,
                            ToJson<Sectionresourceuser>(srusers),
                            dtBail,
                            ref iStartNext,
                            ref data
                        )
                    )
                        break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data" + iStart.ToString());

            Projdata ProjData = entities.First();
            ProjData.Json = data + FinishData();
            ProjData.StartNext = iStartNext;
            ProjData.SnapshotDate = snapshotDate;
            return entities;
        }

        protected override IQueryable<Projdata> FromStartIndex(
            IQueryable<Projdata>? entities,
            string startIndex,
            string version = "",
            string projectid = ""
        )
        {
            dynamic? x = JsonConvert.DeserializeObject(version);
            int filterVersion = x?.version ?? 1;
            return GetData(entities ?? GetAll(), projectid, startIndex, filterVersion);
        }

        public override IQueryable<Projdata> FromProjectList(
            IQueryable<Projdata>? entities,
            string idList
        )
        {
            IQueryable<Projdata> result = GetData(entities ?? GetAll(), idList, "0");
            return result;
        }

        protected override IQueryable<Projdata> GetAll()
        {
            List<Projdata> entities = new()
            { new Projdata() };
            return entities.AsAsyncQueryable();
        }

        public override IQueryable<Projdata> FromCurrentUser(IQueryable<Projdata>? entities = null)
        {
            return GetAll();
        }
    }
}
