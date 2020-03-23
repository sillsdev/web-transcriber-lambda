using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;

namespace SIL.Transcriber.Repositories
{
    public class OrgDataRepository : BaseRepository<OrgData>
    {
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly OrganizationService organizationService;
        protected readonly GroupMembershipService gmService;
        protected readonly CurrentUserRepository currentUserRepository;

        public OrgDataRepository(
              ILoggerFactory loggerFactory,
              IJsonApiContext jsonApiContext,
              CurrentUserRepository CurrentUserRepository,
              IDbContextResolver contextResolver,
              IJsonApiSerializer jsonSer,
              OrganizationService orgService,
              GroupMembershipService grpMemService
          ) : base(loggerFactory, jsonApiContext, CurrentUserRepository, contextResolver)
        {
            jsonApiSerializer = jsonSer;
            currentUserRepository = CurrentUserRepository;
            organizationService = orgService;
            gmService = grpMemService;
        }

        private bool CheckAdd(int check, object entity, DateTime dtBail,  int start, ref int completed, ref string data)
        {
            Debug.WriteLine(check.ToString() + ":" + DateTime.Now.ToString() + dtBail.ToString());
            if (DateTime.Now > dtBail) return false;
            if (start <= check)
            {
                string thisdata = jsonApiSerializer.Serialize(entity);
                if (data.Length + thisdata.Length > (1000000 * 5.5))
                    return false;
                data += (check > start ?  "," : "") + thisdata;
                completed++;
            }
            return true;
        }

        private async Task<IQueryable<OrgData>> GetData(IQueryable<OrgData> entities, string start)
        {
            string data = "{\"data\":[";
            int iStart;
            if (!int.TryParse(start, out iStart))
                iStart = 0;
            int iStartNext = iStart;
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(10);

            do
            {
                if (!CheckAdd(0, dbContext.Activitystates,dtBail, iStart, ref iStartNext, ref data)) break;
                if (!CheckAdd(1, dbContext.Projecttypes,dtBail, iStart, ref iStartNext, ref data)) break;
                if (!CheckAdd(2, dbContext.Plantypes,dtBail, iStart, ref iStartNext, ref data)) break;
                if (!CheckAdd(3, dbContext.Roles,dtBail, iStart, ref iStartNext, ref data)) break;
                if (!CheckAdd(4, dbContext.Integrations,dtBail, iStart, ref iStartNext, ref data)) break;
                if (!CheckAdd(5, dbContext.Plantypes,dtBail, iStart, ref iStartNext, ref data)) break;

                IEnumerable<Organization> orgs = await organizationService.GetAsync(); //this limits to current user
                if (!CheckAdd(6, orgs,dtBail, iStart, ref iStartNext, ref data)) break;
                IEnumerable<GroupMembership> gms = await gmService.GetAsync(); ;
                if (!CheckAdd(7, gms,dtBail, iStart, ref iStartNext, ref data)) break;
                //invitations
                if (!CheckAdd(8, dbContext.Invitations.Join(orgs, i => i.OrganizationId, o => o.Id, (i, o) => i),dtBail, iStart, ref iStartNext, ref data)) break;
                //groups
                if (!CheckAdd(9, dbContext.Groups.Join(gms, g => g.Id, gm => gm.GroupId, (g, gm) => g),dtBail, iStart, ref iStartNext, ref data)) break;
                //orgmems
                var oms = dbContext.Organizationmemberships.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om).Where(x => !x.Archived);
                if (!CheckAdd(10, oms,dtBail, iStart,  ref iStartNext, ref data)) break;
                //users
                if (!CheckAdd(11, oms.Count() > 0 ? 
                                            dbContext.Users.Join(oms, u => u.Id, om => om.UserId, (u, om) => u).Where(x => !x.Archived) : 
                                            dbContext.Users.Where(x => x.Id == CurrentUser.Id),dtBail, iStart, ref iStartNext, ref data)) break;

                //projects
                IEnumerable<Project> projects = dbContext.Projects.Join(gms, p => p.GroupId, gm => gm.GroupId, (p, gm) => p).Where(x => !x.Archived);
                if (!CheckAdd(12, projects,dtBail, iStart, ref iStartNext, ref data)) break;
                //projectintegrations
                if (!CheckAdd(13, dbContext.Projectintegrations.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl).Where(x => !x.Archived),dtBail, iStart, ref iStartNext, ref data)) break;
                if (iStart == 0) break;
                //plans
                IEnumerable<Plan> plans = dbContext.Plans.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl).Where(x => !x.Archived);
                if (!CheckAdd(14, plans,dtBail, iStart, ref iStartNext, ref data)) break;
                //sections
                var sections = dbContext.Sections.Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s).Where(x => !x.Archived);
                if (!CheckAdd(15, sections,dtBail, iStart, ref iStartNext, ref data)) break;

                var passages = dbContext.Passages.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p).Where(x => !x.Archived);
                if (!CheckAdd(16, passages,dtBail, iStart, ref iStartNext, ref data)) break;
                //mediafiles
                if (!CheckAdd(17, dbContext.Mediafiles.Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m).Where(x => !x.Archived),dtBail, iStart, ref iStartNext, ref data)) break;

                //passagestatechanges
                if (!CheckAdd(18, dbContext.Passagestatechanges.Join(passages, psc => psc.PassageId, p => p.Id, (psc, p) => psc),dtBail, iStart, ref iStartNext, ref data)) break;
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            var orgData = entities.First();
            orgData.Json = data + "]}";
            orgData.Startnext = iStartNext;
            return entities;
        }
        public override IQueryable<OrgData> Get()
        {
            var entities = new List<OrgData>();
            entities.Add(new OrgData());
            return entities.AsQueryable();
        }
        public override IQueryable<OrgData> Filter(IQueryable<OrgData> entities, FilterQuery filterQuery)
        {
            if (filterQuery.Has(DATA_START_INDEX))
            {
                return GetData(entities, filterQuery.Value).Result;
            }
            return entities;
        }
    }
}
