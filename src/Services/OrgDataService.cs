﻿using JsonApiDotNetCore.Data;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Data;
using SIL.Transcriber.Models;
using SIL.Transcriber.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SIL.Transcriber.Services
{
    public class OrgDataService
    {
        protected readonly AppDbContext dbContext;
        protected readonly IJsonApiSerializer jsonApiSerializer;
        protected readonly IJsonApiDeSerializer jsonApiDeSerializer;
        protected readonly OrganizationService organizationService;
        protected readonly GroupMembershipService gmService;
        protected readonly PlanService PlanService;
    

        public OrgDataService(IDbContextResolver contextResolver, IJsonApiSerializer jsonSer, IJsonApiDeSerializer jsonDeser, 
            OrganizationService orgService,
            GroupMembershipService grpMemService)
        {
            this.dbContext = (AppDbContext)contextResolver.GetContext();
            jsonApiSerializer = jsonSer;
            jsonApiDeSerializer = jsonDeser;
            organizationService = orgService;
            gmService = grpMemService;
    }

        public async Task<OrgData> GetAsync()
        {
            var orgs = await organizationService.GetAsync(); //this limits to current user
            string data = "{\"data\":[" + jsonApiSerializer.Serialize(orgs);

            var gms = await gmService.GetAsync();
            data += "," + jsonApiSerializer.Serialize(gms);
            //invitations
            data += ","+ jsonApiSerializer.Serialize(dbContext.Invitations.Join(orgs, i => i.OrganizationId, o => o.Id, (i, o) => i));
            //groups
            data += "," + jsonApiSerializer.Serialize(dbContext.Groups.Join(orgs, g => g.OwnerId, o => o.Id, (g, o) => g));
            //orgmems
            var oms = dbContext.Organizationmemberships.Join(orgs, om => om.OrganizationId, o => o.Id, (om, o) => om);
            data += "," + jsonApiSerializer.Serialize(oms);
            //users
            data += "," + jsonApiSerializer.Serialize(dbContext.Users.Join(oms, u => u.Id, om => om.UserId, (u, om) => u));
            //projects
            var projects = dbContext.Projects.Join(orgs, p => p.OrganizationId, o => o.Id, (p, o) => p);
            data += "," + jsonApiSerializer.Serialize(projects);
            //projectintegrations
            data += "," + jsonApiSerializer.Serialize(dbContext.Projectintegrations.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl));
            //plans
            var plans = dbContext.Plans.Join(projects, pl => pl.ProjectId, p => p.Id, (pl, p) => pl);
            data += "," + jsonApiSerializer.Serialize(plans);
            var sections = dbContext.Sections.Join(plans, s => s.PlanId, pl => pl.Id, (s, pl) => s);
            data += "," + jsonApiSerializer.Serialize(sections);
            var passages = dbContext.Passages.Join(sections, p => p.SectionId, s => s.Id, (p, s) => p);
            data += "," + jsonApiSerializer.Serialize(passages);
            var mediafiles = dbContext.Mediafiles.Join(plans, m => m.PlanId, pl => pl.Id, (m, pl) => m);
            data += "," + jsonApiSerializer.Serialize(mediafiles);
            //passagestatechanges
            data += "," + jsonApiSerializer.Serialize(dbContext.Passagestatechanges.Join(passages, psc => psc.PassageId, p => p.Id, (psc, p) => psc));

            data += "," + jsonApiSerializer.Serialize(dbContext.Activitystates);
            data += "," + jsonApiSerializer.Serialize(dbContext.Projecttypes);
            data += "," + jsonApiSerializer.Serialize(dbContext.Plantypes);
            data += "," + jsonApiSerializer.Serialize(dbContext.Roles);
            data += "," + jsonApiSerializer.Serialize(dbContext.Integrations);
            data += "," + jsonApiSerializer.Serialize(dbContext.Plantypes);

            var ret = new OrgData(data + "]}");
            return ret;

        }
    }
}