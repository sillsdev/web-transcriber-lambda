﻿using JsonApiDotNetCore.Internal.Query;
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

namespace SIL.Transcriber.Repositories
{
    public class ProjDataRepository : BaseRepository<ProjData>
    {
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly OrganizationService organizationService;
        protected readonly GroupMembershipService gmService;

        public ProjDataRepository(
              ILoggerFactory loggerFactory,
              IJsonApiContext jsonApiContext,
              CurrentUserRepository CurrentUserRepository,
              AppDbContextResolver contextResolver,
              IJsonApiSerializer jsonSer,
              OrganizationService orgService,
              GroupMembershipService grpMemService
          ) : base(loggerFactory, jsonApiContext, CurrentUserRepository, contextResolver)
        {
            jsonApiSerializer = jsonSer;
            organizationService = orgService;
            gmService = grpMemService;
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

        private IQueryable<ProjData> GetData(IQueryable<ProjData> entities, string project, string start)
        {
            string data = "";
            int iStart;
            if (!int.TryParse(start, out iStart))
                iStart = 0;
            int iStartNext = iStart;
            int projectid;
            if (!int.TryParse(project, out projectid))
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
                if (!CheckAdd(3, dbContext.Mediafiles.Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m).Where(x => !x.Archived), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                //passagestatechanges
                if (!CheckAdd(4, dbContext.Passagestatechanges.Join(passages, psc => psc.PassageId, p => p.Id, (psc, p) => psc), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
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
            List<ProjData> entities = new List<ProjData>();
            entities.Add(new ProjData());
            return entities.AsQueryable();
        }
        public override IQueryable<ProjData> Filter(IQueryable<ProjData> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(PROJECT_SEARCH_TERM))
            {
                return GetData(entities, filterQuery.Value, "0");
            }
            return entities;
        }
    }
}
