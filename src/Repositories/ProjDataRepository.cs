using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Repositories
{
    public class ProjDataRepository : BaseRepository<ProjData>
    {
        protected int filterVersion = 0;
        protected string filterStart = "";
        protected string filterProject = "";
        protected readonly SectionService SectionService;
        protected readonly SectionRepository SectionRepository;

        public ProjDataRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SectionService sectionService,
            SectionRepository sectionRepository
          ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
              constraintProviders, loggerFactory,resourceDefinitionAccessor, currentUserRepository)
        {
            SectionService = sectionService;
            SectionRepository = sectionRepository;
        }

        private bool CheckAdd(int check, object entity, DateTime dtBail, int start, ref int completed, ref string data)
        {
            Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail) return false;
            if (start <= check)
            {
                string thisdata = System.Text.Json.JsonSerializer.Serialize(entity, Options);
                if (data.Length + thisdata.Length > (1000000 * 4))
                    return false;
                data += (check == start ? "" : ",") + thisdata;
                completed++;
            }
            return true;
        }

        private IQueryable<ProjData> GetData(IQueryable<ProjData> entities, string project, string start, int version = 1)
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
                IEnumerable<Plan> plans = dbContext.Plans.Where(x => x.ProjectId == projectid && !x.Archived);
                //if (!CheckAdd(0, plans, dtBail, Serializer, ref iStartNext, ref data)) break;

                //sections
                IQueryable<Section> sections = dbContext.Sections
                   // .Include(s=>s.Plan).Include(s=>s.Editor).Include(s=>s.Transcriber).Include(s=>s.LastModifiedByUser)
                    .Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s).Where(x => !x.Archived);
                if (!CheckAdd(1, sections, dtBail,  ref iStartNext, ref data)) break;
                
                //passages
                IQueryable<Passage> passages = dbContext.Passages.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p).Where(x => !x.Archived);
                if (!CheckAdd(2, passages, dtBail,  ref iStartNext, ref data)) break;

                //mediafiles
                IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m).Where(x => !x.Archived);
                if (!CheckAdd(3, mediafiles, dtBail,  ref iStartNext, ref data)) break;

                //passagestatechanges
                if (!CheckAdd(4, dbContext.Passagestatechanges.Join(passages, psc => psc.PassageId, p => p.Id, (psc, p) => psc), dtBail,  ref iStartNext, ref data)) break;
                if (version > 3)
                {
                    //discussions
                    IQueryable<Discussion> discussions = dbContext.Discussions.Join(mediafiles, d => d.MediafileId, m => m.Id, (d, m) => d).Where(x => !x.Archived);
                    if (!CheckAdd(5, discussions, dtBail,  ref iStartNext, ref data)) break;

                    //comments
                    if (!CheckAdd(6, dbContext.Comments.Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c).Where(x => !x.Archived), dtBail,  ref iStartNext, ref data)) break;
                    
                    IQueryable<SectionResource> sectionresources = dbContext.Sectionresources.Join(sections, sr => sr.SectionId, s => s.Id, (sr, s) => sr).Where(x => !x.Archived);
                    if (!CheckAdd(7, sectionresources, dtBail,   ref iStartNext, ref data)) break;
                   
                    IQueryable<SectionResourceUser> srusers = dbContext.Sectionresourceusers.Join(sectionresources, u => u.SectionResourceId, sr => sr.Id, (u, sr) => u).Where(x => !x.Archived);
                    if (!CheckAdd(8, srusers, dtBail,  ref iStartNext, ref data)) break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            ProjData ProjData = entities.First();
            ProjData.Json = data + FinishData();
            ProjData.StartIndex = iStartNext;
            ProjData.SnapshotDate = snapshotDate;
            return entities;
        }
        private void ResetFilters()
        {
            filterStart = "";
            filterVersion = 0;
            filterProject = "";
        }
        protected override IQueryable<ProjData> FromStartIndex(QueryLayer layer, string startIndex, string version="", string projectid = "")
        {
            filterStart = startIndex;
            /*            if (filterVersion == 0 && jsonApiContext.QuerySet.Filters.Find(f => f.Attribute.ToLower() == VERSION) == null)
            {
                filterVersion = 1;
            }*/
            if (filterVersion > 0)
            {
                IQueryable<ProjData> result = GetData(GetAll(),projectid, filterStart,filterVersion);
                filterStart = "";
                filterVersion = 0;
                return result;
            }
            return GetAll();
        }
        /*
        protected override IQueryable<ProjData> FromVersion(QueryLayer layer, string version, string projectid = "")
        {
            //if I'm first...just remember my value
            dynamic? x = JsonConvert.DeserializeObject(version);
            filterVersion = x?.version??1;
           
            if (filterStart != "")
            {
                IQueryable<ProjData> result = GetData(GetAll(), filterProject, filterStart, filterVersion);
                ResetFilters();
                return result;
            }
            return GetAll();
        }
        */
        protected override IQueryable<ProjData> FromProjectList(QueryLayer layer, string idList)
        {
            filterProject = idList;
            IQueryable<ProjData> result = GetData(GetAll(), filterProject, filterStart, filterVersion);
            ResetFilters();
            return result;
        }
        protected override IQueryable<ProjData> GetAll()
        {
            List<ProjData> entities = new List<ProjData>
            {
                new ProjData()
            };
            return entities.AsAsyncQueryable();
        }
        protected override IQueryable<ProjData> FromCurrentUser(QueryLayer? layer = null)
        {
            return GetAll();
        }


    }
}
