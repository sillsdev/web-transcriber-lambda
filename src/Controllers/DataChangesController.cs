
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DatachangesController : ControllerBase
    {
        GroupMembershipService GMService;
        GroupService GroupService;
        InvitationService InvitationService;
        MediafileService MediafileService;
        OrganizationMembershipService OrgMemService;
        OrganizationService OrganizationService;
        PassageSectionService PSService;
        PassageService PassageService;
        PlanService PlanService;
        IResourceService<ProjectIntegration> ProjIntService;
        ProjectService ProjectService;
        SectionService SectionService;
        UserService UserService;
        ILoggerFactory LoggerFactory;
        ICurrentUserContext CurrentUserContext;
        IJsonApiContext JsonApiContext;

        public DatachangesController(
            ILoggerFactory loggerFactory,
            ICurrentUserContext currentUserContext,
            IJsonApiContext jsonApiContext,
            GroupMembershipService gmService,
            GroupService groupService,
            InvitationService invitationService,
            MediafileService mediafileService,
            OrganizationMembershipService omService,
            OrganizationService organizationService,
            PassageSectionService psService,
            PassageService passageService,
            PlanService planService,
            IResourceService<ProjectIntegration> piService,
            ProjectService projectService,
            SectionService sectionService,
            UserService userService)
         : base()
        {
            LoggerFactory = loggerFactory;
            CurrentUserContext = currentUserContext;
            JsonApiContext = jsonApiContext;
            GMService = gmService;
            GroupService = groupService;
            InvitationService = invitationService;
            MediafileService = mediafileService;
            OrgMemService = omService;
            OrganizationService = organizationService;
            PSService = psService;
            PassageService = passageService;
            PlanService = planService;
            ProjIntService = piService;
            ProjectService = projectService;
            SectionService = sectionService;
            UserService = userService;
        }
        private void BuildList(IEnumerable<BaseModel> recs, string type, List<OrbitId[]> addTo)
        {
            List<OrbitId> tblList = new List<OrbitId>();

            foreach (BaseModel m in recs)
            {
                tblList.Add(new OrbitId() { type = type, id = m.Id });
            }
            if (tblList.Count > 0)
                addTo.Add(tblList.ToArray());
        }
        [HttpGet("{since}")]
        public ActionResult GetDataChanges([FromRoute] string since)
        {
            DateTime dtSince;
            if (!DateTime.TryParse(since, out dtSince))
                return new UnprocessableEntityResult();

            List<OrbitId[]> changes = new List<OrbitId[]>();
            List<OrbitId[]> deleted = new List<OrbitId[]>();
            DateTime dtNow = DateTime.UtcNow;
            var pc = new PassagesController(LoggerFactory, JsonApiContext, PassageService, CurrentUserContext, OrganizationService, UserService);

            BuildList(GMService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "groupmembership", changes);
            BuildList(GMService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "groupmembership", deleted);

            BuildList(GroupService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "group", changes);
            BuildList(GroupService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "group", deleted);

            BuildList(InvitationService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "invitation", changes);
            //BuildList(InvitationService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "invitation", deleted);

            BuildList(MediafileService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "mediafile", changes);
            BuildList(MediafileService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "mediafile", deleted);

            BuildList(OrgMemService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "organizationmembership", changes);
            BuildList(OrgMemService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "organizationmembership", deleted);

            BuildList(OrganizationService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "organization", changes);
            BuildList(OrganizationService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "organization", deleted);

            BuildList(PSService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "passagesection", changes);
            BuildList(PSService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "passagesection", deleted);

            BuildList(PassageService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "passage", changes);
            BuildList(PassageService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "passage", deleted);

            BuildList(PlanService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "plan", changes);
            BuildList(PlanService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "plan", deleted);

            BuildList(ProjIntService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "projectintegration", changes);
            //BuildList(ProjIntService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "projectintegration", deleted);

            BuildList(ProjectService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "project", changes);
            BuildList(ProjectService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "project", deleted);

            BuildList(SectionService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "section", changes);
            BuildList(SectionService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "section", deleted);

            BuildList(UserService.GetAsync().Result.Where(p => p.DateUpdated > dtSince), "user", changes);
            BuildList(UserService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "user", deleted);

            var ret = new DataChanges() { Id = 1,  Querydate = dtNow, Changes = changes.ToArray(), Deleted = deleted.ToArray() };
            return Ok(ret);
        }
    }
}