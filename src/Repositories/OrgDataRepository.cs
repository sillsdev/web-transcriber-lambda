using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Linq;
using System.Threading.Tasks;
using static SIL.Transcriber.Utility.Extensions.JSONAPI.FilterQueryExtensions;
using SIL.Transcriber.Data;

namespace SIL.Transcriber.Repositories
{
    public class OrgDataRepository : BaseRepository<OrgData>
    {
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly OrganizationService organizationService;
        protected readonly GroupMembershipService gmService;
        protected readonly IJsonApiContext jsonApiContext;
        protected int filterVersion = 0;
        protected string filterStart="";

        public OrgDataRepository(
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

        private async Task<IQueryable<OrgData>> GetData(IQueryable<OrgData> entities, string start, int version = 1)
        {
            string data = "";
            if (!int.TryParse(start, out int iStart))
            {
                iStart = 0;
            }

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

                if (version > 3)
                {
                    if (!CheckAdd(15, dbContext.Workflowsteps.Where(x => !x.Archived), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                    IEnumerable<int> ids = orgs.Select(o => o.Id);
                    IQueryable<ArtifactCategory> cats = dbContext.Artifactcategorys.Where(c => (c.OrganizationId == null || ids.Contains((int)c.OrganizationId)) && !c.Archived);
                    if (!CheckAdd(16, cats, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                    IQueryable<ArtifactType> typs = dbContext.Artifacttypes.Where(c => (c.OrganizationId == null || ids.Contains((int)c.OrganizationId)) && !c.Archived);
                    if (!CheckAdd(17, typs, dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                    if (!CheckAdd(18, dbContext.Orgworkflowsteps.Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c).Where(x => !x.Archived), dtBail, jsonApiSerializer, ref iStartNext, ref data)) break;
                }
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
            List<OrgData> entities = new List<OrgData>
            {
                new OrgData()
            };
            return entities.AsQueryable();
        }
        public override IQueryable<OrgData> Filter(IQueryable<OrgData> entities, FilterQuery filterQuery)
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
                if (filterStart != "")
                {
                    IQueryable<OrgData> result = GetData(entities, filterStart, filterVersion).Result;
                    filterStart = "";
                    filterVersion = 0;
                    return result;
                }
                return entities;
            }
            if (filterQuery.Has(DATA_START_INDEX))
            {
                QuerySet query = jsonApiContext.QuerySet;
                if (filterVersion == 0 && query.Filters.Find(f => f.Attribute.ToLower() == VERSION) != null)
                {
                    filterStart = filterQuery.Value;
                    return entities;
                }
                IQueryable<OrgData> result = GetData(entities, filterStart, filterVersion).Result;
                filterStart = "";
                filterVersion = 0;
                return result;
            }
            return entities;
        }
    }
}
