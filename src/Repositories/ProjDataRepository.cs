using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using SIL.Transcriber.Services;
using SIL.Transcriber.Serialization;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;

namespace SIL.Transcriber.Repositories
{
    public class ProjDataRepository : BaseRepository<Projdata>
    {
        protected readonly SectionService SectionService;
        protected readonly SectionRepository SectionRepository;
        protected readonly MyResponseModelAdapter ResponseModelAdapter;

        public ProjDataRepository(
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            SectionService sectionService,
            SectionRepository sectionRepository,
             MyResponseModelAdapter responseModelAdapter
          ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
              constraintProviders, loggerFactory,resourceDefinitionAccessor, currentUserRepository)
        {
            SectionService = sectionService;
            SectionRepository = sectionRepository;
            ResponseModelAdapter = responseModelAdapter;
        }

        private IQueryable<Projdata> GetData(IQueryable<Projdata> entities, string project, string start, int version = 1)
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
                IEnumerable<Plan> plans = dbContext.PlansData.Where(x => x.ProjectId == projectid && !x.Archived);
                //if (!CheckAdd(0, plans, dtBail, Serializer, ref iStartNext, ref data)) break;

                //sections
                IQueryable<Section> sections = dbContext.SectionsData
                    .Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s).Where(x => !x.Archived);
                if (!CheckAdd(1, SerializeIt(sections, ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;
                
                //passages
                IQueryable<Passage> passages = dbContext.PassagesData.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p).Where(x => !x.Archived);
                if (!CheckAdd(2, SerializeIt(passages, ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;

                //mediafiles
                IQueryable<Mediafile> mediafiles = dbContext.MediafilesData.Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m).Where(x => !x.Archived);
                if (!CheckAdd(3, SerializeIt(mediafiles, ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;

                //passagestatechanges
                if (!CheckAdd(4, SerializeIt(dbContext.PassagestatechangesData.Join(passages, psc => psc.PassageId, p => p.Id, (psc, p) => psc), ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;
                if (version > 3)
                {
                    //discussions
                    IQueryable<Discussion> discussions = dbContext.DiscussionsData.Join(mediafiles, d => d.MediafileId, m => m.Id, (d, m) => d).Where(x => !x.Archived);
                    if (!CheckAdd(5, SerializeIt(discussions, ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;

                    //comments
                    if (!CheckAdd(6, SerializeIt(dbContext.CommentsData.Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c).Where(x => !x.Archived), ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;
                    
                    IQueryable<Sectionresource> sectionresources = dbContext.SectionresourcesData.Join(sections, sr => sr.SectionId, s => s.Id, (sr, s) => sr).Where(x => !x.Archived);
                    if (!CheckAdd(7, SerializeIt(sectionresources, ResponseModelAdapter), dtBail,   ref iStartNext, ref data)) break;
                   
                    IQueryable<Sectionresourceuser> srusers = dbContext.SectionresourceusersData.Join(sectionresources, u => u.SectionResourceId, sr => sr.Id, (u, sr) => u).Where(x => !x.Archived);
                    if (!CheckAdd(8, SerializeIt(srusers, ResponseModelAdapter), dtBail,  ref iStartNext, ref data)) break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            Projdata ProjData = entities.First();
            ProjData.Json = data + FinishData();
            ProjData.StartIndex = iStartNext;
            ProjData.SnapshotDate = snapshotDate;
            return entities;
        }
        protected override IQueryable<Projdata> FromStartIndex(IQueryable<Projdata>? entities, string startIndex, string version="", string projectid = "")
        {
            dynamic? x = JsonConvert.DeserializeObject(version);
            int filterVersion = x?.version ?? 1;
            return GetData(entities??GetAll(), projectid, startIndex, filterVersion);
        }

        protected override IQueryable<Projdata> FromProjectList(IQueryable<Projdata>? entities, string idList)
        {
            IQueryable<Projdata> result = GetData(entities??GetAll(), idList, "0");
            return result;
        }
        protected override IQueryable<Projdata> GetAll()
        {
            List<Projdata> entities = new List<Projdata>
            {
                new Projdata()
            };
            return entities.AsAsyncQueryable();
        }
        public override IQueryable<Projdata> FromCurrentUser(IQueryable<Projdata>? entities = null)
        {
            return GetAll();
        }


    }
}
