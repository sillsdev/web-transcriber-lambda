using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Data;
using Newtonsoft.Json;

namespace SIL.Transcriber.Repositories
{
    public class ProjDataRepository : BaseRepository<ProjData>
    {
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly OrganizationService organizationService;
        protected readonly GroupMembershipService gmService;
        protected readonly IJsonApiContext jsonApiContext;
        protected int filterVersion = 0;
        protected string filterStart = "";
        protected string filterProject = "";
        public ProjDataRepository(
              ILoggerFactory loggerFactory,
              IJsonApiContext JsonApiContext,
              CurrentUserRepository CurrentUserRepository,
              AppDbContextResolver contextResolver,
              IJsonApiSerializer jsonSer,
              OrganizationService orgService,
              GroupMembershipService grpMemService
          ) : base(loggerFactory, JsonApiContext, CurrentUserRepository, contextResolver)
        {
            jsonApiSerializer = jsonSer;
            organizationService = orgService;
            gmService = grpMemService;
            jsonApiContext = JsonApiContext;
        }

        private bool CheckAdd(int check, object entity, DateTime dtBail, int start, ref int completed, ref string data)
        {
            Logger.LogInformation($"{check} : {DateTime.Now} {dtBail}");
            if (DateTime.Now > dtBail) return false;
            if (start <= check)
            {
                string thisdata = jsonApiSerializer.Serialize(entity);
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
                IEnumerable<Plan> plans = dbContext.Plans.Where(pl => pl.ProjectId == projectid && !pl.Archived);
                if (!CheckAdd(0, plans, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                //sections
                IQueryable<Section> sections = dbContext.Sections.Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s).Where(x => !x.Archived);
                if (!CheckAdd(1, sections, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                
                //passages
                IQueryable<Passage> passages = dbContext.Passages.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p).Where(x => !x.Archived);
                if (!CheckAdd(2, passages, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                //mediafiles
                IQueryable<Mediafile> mediafiles = dbContext.Mediafiles.Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m).Where(x => !x.Archived);
                if (!CheckAdd(3, mediafiles, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                //passagestatechanges
                if (!CheckAdd(4, dbContext.Passagestatechanges.Join(passages, psc => psc.PassageId, p => p.Id, (psc, p) => psc), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                if (version > 3)
                {
                    //discussions
                    IQueryable<Discussion> discussions = dbContext.Discussions.Join(mediafiles, d => d.MediafileId, m => m.Id, (d, m) => d).Where(x => !x.Archived);
                    if (!CheckAdd(5, discussions, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                    //comments
                    if (!CheckAdd(6, dbContext.Comments.Join(discussions, c => c.DiscussionId, d => d.Id, (c, d) => c).Where(x => !x.Archived), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            ProjData ProjData = entities.FirstOrDefault();
            ProjData.Json = data + FinishData();
            ProjData.Startnext = iStartNext;
            ProjData.SnapshotDate = snapshotDate;
            return entities;
        }
        public override IQueryable<ProjData> Get()
        {
            List<ProjData> entities = new List<ProjData>
            {
                new ProjData()
            };
            return entities.AsQueryable();
        }
        private void ResetFilters()
        {
            filterStart = "";
            filterVersion = 0;
            filterProject = "";
        }
        private IQueryable<ProjData> ApplyFilter(IQueryable<ProjData> entities)
        {
            if (filterVersion == 0 && jsonApiContext.QuerySet.Filters.Find(f => f.Attribute.ToLower() == VERSION) == null)
            {
                filterVersion = 1;
            }
            if (filterStart != "" && filterProject != "" && filterVersion != 0)
            {
                IQueryable<ProjData> result = GetData(entities, filterProject, filterStart, filterVersion);
                ResetFilters();
                return result;
            }
            return entities;
        }
        public override IQueryable<ProjData> Filter(IQueryable<ProjData> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(VERSION))
            {
                //if I'm first...just remember my value
                dynamic x = JsonConvert.DeserializeObject(filterQuery.Value);
                try
                {
                    filterVersion = x.version;
                }
                catch
                {
                    filterVersion = 1;
                }
                return ApplyFilter(entities);
            }
            if (filterQuery.Has(DATA_START_INDEX))
            {
                filterStart = filterQuery.Value;
                return ApplyFilter(entities);
            }
            if (filterQuery.Has(PROJECT_SEARCH_TERM))
            {
                filterProject = filterQuery.Value;
                return ApplyFilter(entities);
            }
            return entities;

        }
    }
}
