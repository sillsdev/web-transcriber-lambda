using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using SIL.Transcriber.Data;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Resources;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using JsonApiDotNetCore.Serialization.Response;
using System.Text.Json;
using SIL.Transcriber.Serializers;

namespace SIL.Transcriber.Repositories
{
    public class OrgDataRepository : BaseRepository<Orgdata>
    {
        protected readonly OrganizationRepository organizationRepository;
        protected readonly GroupMembershipRepository gmRepository;
        protected readonly IResourceGraph _resourceGraph;
        protected readonly IResourceFactory _resourceFactory;
        protected readonly IEnumerable<IQueryConstraintProvider> _constraintProviders;
        //protected readonly JsonApiWriter _JsonApiWriter;
        //protected readonly IResponseModelAdapter _JsonApiWriter;
        //readonly private HttpContext? HttpContext;
        protected readonly JsonSerializerOptions Options = new()
        {
            // WriteIndented = true,
            //PropertyNamingPolicy = new CamelToDashNamingPolicy(),
        };
        public OrgDataRepository(
            IHttpContextAccessor httpContextAccessor,
            ITargetedFields targetedFields, AppDbContextResolver contextResolver,
            IResourceGraph resourceGraph, IResourceFactory resourceFactory,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            ILoggerFactory loggerFactory,
            IResourceDefinitionAccessor resourceDefinitionAccessor,
            CurrentUserRepository currentUserRepository,
            OrganizationRepository orgRepository,
            GroupMembershipRepository grpMemRepository
            //IResponseModelAdapter jsonapiWriter
          ) : base(targetedFields, contextResolver, resourceGraph, resourceFactory, 
              constraintProviders, loggerFactory,resourceDefinitionAccessor, currentUserRepository)
        {
            organizationRepository = orgRepository;
            gmRepository = grpMemRepository;
            _resourceGraph = resourceGraph;
            _resourceFactory = resourceFactory;
            _constraintProviders = constraintProviders;
            //_JsonApiWriter = jsonapiWriter;
            //HttpContext = httpContextAccessor.HttpContext;
            //Options.Converters.Add(new ResourceObjectConverter(resourceGraph));
            Options.Converters.Add(new TranscriberConverter());
        }

        private IQueryable<Orgdata> GetData(IQueryable<Orgdata> entities, string start, CancellationToken cancellationToken, int version = 1)
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
                IQueryable<Organization> orgs = organizationRepository.GetMine(); // await organizationService.GetAsync(cancellationToken); //this limits to current use

                //var test = _JsonApiWriter.Convert(new Orgdata { StartIndex = 4});
                var test2 = System.Text.Json.JsonSerializer.Serialize(new Organization { Name = "test"}, Options);

                if (!CheckAdd(0, dbContext.Activitystates.ToList(), dtBail, ref iStartNext, ref data)) break;
                if (!CheckAdd(1, dbContext.Projecttypes.ToList(), dtBail, ref iStartNext, ref data)) break;
                if (!CheckAdd(2, dbContext.Plantypes.ToList(), dtBail, ref iStartNext, ref data)) break;
                if (!CheckAdd(3, dbContext.Roles.ToList(), dtBail,  ref iStartNext, ref data)) break;
                if (!CheckAdd(4, dbContext.Integrations.ToList(), dtBail,  ref iStartNext, ref data)) break;

                if (!CheckAdd(6, orgs.ToList(), dtBail,  ref iStartNext, ref data)) break;
                IQueryable<GroupMembership> gms = gmRepository.GetMine();
                if (!CheckAdd(7, gms.ToList(), dtBail,  ref iStartNext, ref data)) break;
                //invitations
                if (!CheckAdd(8, dbContext.Invitations.Join(orgs, i => i.OrganizationId, o => o.Id, (i, o) => i).ToList(), dtBail, ref iStartNext, ref data)) break;
                //groups
                if (!CheckAdd(9, dbContext.Groups.Join(gms, g => g.Id, gm => gm.GroupId, (g, gm) => g).ToList(), dtBail, ref iStartNext, ref data)) break;
                //orgmems
                IQueryable<OrganizationMembership> oms = dbContext.Organizationmemberships.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om).Where(x => !x.Archived);
                if (!CheckAdd(10, oms.ToList(), dtBail,  ref iStartNext, ref data)) break;
                int userid = CurrentUser?.Id ?? -1;
                //users
                if (!CheckAdd(11, oms.Any() ?
                                            dbContext.Users.Join(oms, u => u.Id, om => om.UserId, (u, om) => u).Where(x => !x.Archived).ToList() :
                                            dbContext.Users.Where(x => x.Id == userid).ToList(), dtBail, ref iStartNext, ref data)) break;

                //projects
                IEnumerable<Project> projects = dbContext.Projects.Join(gms, p => p.GroupId, gm => gm.GroupId, (p, gm) => p).Where(x => !x.Archived);
                if (!CheckAdd(12, projects.ToList(), dtBail,  ref iStartNext, ref data)) break;
                //projectintegrations
                if (!CheckAdd(13, dbContext.Projectintegrations.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl).Where(x => !x.Archived).ToList(), dtBail, ref iStartNext, ref data)) break;
                //plans
                if (!CheckAdd(14, dbContext.Plans.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl).Where(x => !x.Archived).ToList(), dtBail, ref iStartNext, ref data)) break;

                if (version > 3)
                {
                    if (!CheckAdd(15, dbContext.Workflowsteps.Where(x => !x.Archived).ToList(), dtBail,  ref iStartNext, ref data)) break;
                    IEnumerable<int> ids = orgs.Select(o => o.Id);
                    IQueryable<Artifactcategory> cats = dbContext.Artifactcategorys.Where(c => (c.OrganizationId == null || ids.Contains((int)c.OrganizationId)) && !c.Archived);
                    if (!CheckAdd(16, cats.ToList(), dtBail,  ref iStartNext, ref data)) break;
                    IQueryable<Artifacttype> typs = dbContext.Artifacttypes.Where(c => (c.OrganizationId == null || ids.Contains((int)c.OrganizationId)) && !c.Archived);
                    if (!CheckAdd(17, typs.ToList(), dtBail,  ref iStartNext, ref data)) break;
                    if (!CheckAdd(18, dbContext.Orgworkflowsteps.Join(orgs, c => c.OrganizationId, o => o.Id, (c, o) => c).Where(x => !x.Archived).ToList(), dtBail, ref iStartNext, ref data)) break;
                }
                iStartNext = -1; //Done!
            } while (false); //do it once
            if (iStart == iStartNext)
                throw new System.Exception("Single table is too large to return data");

            orgData.Json = data + FinishData();
            orgData.StartIndex = iStartNext;
            return entities;
        }

        protected override IQueryable<Orgdata> GetAll()
        {
            List<Orgdata> entities = new()
            {
                new Orgdata()
            };
            return entities.AsAsyncQueryable();
        }
        protected override IQueryable<Orgdata> FromCurrentUser(QueryLayer? layer = null)
        {
            return GetAll();
        }
        protected override IQueryable<Orgdata> FromProjectList(QueryLayer layer, string idList)
        {
            return GetAll();
        }
        protected override IQueryable<Orgdata> FromStartIndex(QueryLayer layer, string startIndex, string version ="", string projectid = "")
        {
            dynamic? x = JsonConvert.DeserializeObject(version);
            int filterVersion = x?.version ?? 1;

            IQueryable<Orgdata> result = GetData(GetAll(), startIndex, new CancellationToken(), filterVersion);
            return result;
        }
        /*
        protected override IQueryable<Orgdata> FromVersion(QueryLayer layer, string version, string projectid = "")
        {
            //if I'm first...just remember my value
            dynamic? x = JsonConvert.DeserializeObject(version);
            filterVersion = x?.version??1;
            
            if (filterStart != "")
            {
                IQueryable<Orgdata> result = GetData(GetAll(), filterStart, new CancellationToken(), filterVersion);
                filterStart = "";
                filterVersion = 0;
                return result;
            }
            return GetAll();
        }
        */
    }

         //TODO
/*
public override IQueryable<OrgData> Filter(IQueryable<OrgData> entities, FilterQuery filterQuery)
{
    if (filterQuery.Has(DATA_START_INDEX))
    {
        return GetData(entities, filterQuery.Value).Result;
    }
} */
}