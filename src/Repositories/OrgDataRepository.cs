using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
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
            organizationService = orgService;
            gmService = grpMemService;
        }

        private async Task<IQueryable<OrgData>> GetData(IQueryable<OrgData> entities, string start)
        {
            string data = "";
            int iStart;
            if (!int.TryParse(start, out iStart))
                iStart = 0;
            int iStartNext = iStart;
            //give myself 20 seconds to get as much as I can...
            DateTime dtBail = DateTime.Now.AddSeconds(20);

            do
            {
                if (!CheckAdd(0, dbContext.Activitystates, dtBail, jsonApiSerializer,  ref iStartNext, ref data)) break;
                if (!CheckAdd(1, dbContext.Projecttypes,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                if (!CheckAdd(2, dbContext.Plantypes,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                if (!CheckAdd(3, dbContext.Roles,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                if (!CheckAdd(4, dbContext.Integrations,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                if (!CheckAdd(5, dbContext.Plantypes,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                IEnumerable<Organization> orgs = await organizationService.GetAsync(); //this limits to current user
                if (!CheckAdd(6, orgs, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                IEnumerable<GroupMembership> gms = await gmService.GetAsync(); ;
                if (!CheckAdd(7, gms,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                //invitations
                if (!CheckAdd(8, dbContext.Invitations.Join(orgs, i => i.OrganizationId, o => o.Id, (i, o) => i),dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                //groups
                if (!CheckAdd(9, dbContext.Groups.Join(gms, g => g.Id, gm => gm.GroupId, (g, gm) => g),dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                //orgmems
                IQueryable<OrganizationMembership> oms = dbContext.Organizationmemberships.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om).Where(x => !x.Archived);
                if (!CheckAdd(10, oms,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                //users
                if (!CheckAdd(11, oms.Count() > 0 ? 
                                            dbContext.Users.Join(oms, u => u.Id, om => om.UserId, (u, om) => u).Where(x => !x.Archived) : 
                                            dbContext.Users.Where(x => x.Id == CurrentUser.Id),dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;

                //projects
                IEnumerable<Project> projects = dbContext.Projects.Join(gms, p => p.GroupId, gm => gm.GroupId, (p, gm) => p).Where(x => !x.Archived);
                if (!CheckAdd(12, projects,dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                //projectintegrations
                if (!CheckAdd(13, dbContext.Projectintegrations.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl).Where(x => !x.Archived),dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                //plans
                if (!CheckAdd(14, dbContext.Plans.Join(projects, pl => pl.ProjectId, p => p.Id, (pl,p) => pl).Where(x => !x.Archived), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            OrgData orgData = entities.FirstOrDefault();
            orgData.Json = data + FinishData();
            orgData.Startnext = iStartNext;
            return entities;
        }
        public override IQueryable<OrgData> Get()
        {
            List<OrgData> entities = new List<OrgData>();
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
