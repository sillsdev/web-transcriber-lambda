using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SIL.Transcriber.Models;
using SIL.Transcriber.Services;
using SIL.Transcriber.Utility;

namespace SIL.Transcriber.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class DatachangesController : BaseController<DataChanges> 
    {
        GroupMembershipService GMService;
        GroupService GroupService;
        InvitationService InvitationService;
        MediafileService MediafileService;
        OrganizationMembershipService OrgMemService;
        OrganizationService OrganizationService;
        PassageService PassageService;
        PlanService PlanService;
        ProjectIntegrationService ProjIntService;
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
            IResourceService<DataChanges> resourceService,
            GroupMembershipService gmService,
            GroupService groupService,
            InvitationService invitationService,
            MediafileService mediafileService,
            OrganizationMembershipService omService,
            OrganizationService organizationService,
            PassageService passageService,
            PlanService planService,
            ProjectIntegrationService piService,
            ProjectService projectService,
            SectionService sectionService,
            UserService userService)
         : base(loggerFactory, jsonApiContext,resourceService,currentUserContext,organizationService,userService)
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
        [HttpGet("since/{since}")]
        public ActionResult GetDataChanges([FromRoute] string since, string origin)
        {
            DateTime dtSince;
            if (!DateTime.TryParse(since, out dtSince))
                return new UnprocessableEntityResult();
            dtSince = dtSince.ToUniversalTime();
            List<OrbitId[]> changes = new List<OrbitId[]>();
            List<OrbitId[]> deleted = new List<OrbitId[]>();
            DateTime dtNow = DateTime.UtcNow;
            int currentUser = CurrentUser.Id;

            BuildList(GMService.GetChanges(currentUser, origin, dtSince), "groupmembership", changes);
            BuildList(GMService.GetDeletedSince(currentUser, origin, dtSince), "groupmembership", deleted);

            BuildList(GroupService.GetChanges(currentUser, origin, dtSince), "group", changes);
            BuildList(GroupService.GetDeletedSince(currentUser, origin, dtSince), "group", deleted);

            BuildList(InvitationService.GetChanges(currentUser, origin, dtSince), "invitation", changes);
            //BuildList(InvitationService.GetDeleted().Result.Where(p => (p.LastModifiedBy != CurrentUser.Id || p.LastModifiedOrigin != HttpContext.GetOrigin()) && p.DateUpdated > dtSince), "invitation", deleted);

            BuildList(MediafileService.GetChanges(currentUser, origin, dtSince), "mediafile", changes);
            BuildList(MediafileService.GetDeletedSince(currentUser, origin, dtSince), "mediafile", deleted);

            BuildList(OrgMemService.GetChanges(currentUser, origin, dtSince), "organizationmembership", changes);
            BuildList(OrgMemService.GetDeletedSince(currentUser, origin, dtSince), "organizationmembership", deleted);

            BuildList(OrganizationService.GetChanges(currentUser, origin, dtSince), "organization", changes);
            BuildList(OrganizationService.GetDeletedSince(currentUser, origin, dtSince), "organization", deleted);

            BuildList(PassageService.GetChanges(currentUser, origin, dtSince), "passage", changes);
            BuildList(PassageService.GetDeletedSince(currentUser, origin, dtSince), "passage", deleted);

            BuildList(PlanService.GetChanges(currentUser, origin, dtSince), "plan", changes);
            BuildList(PlanService.GetDeletedSince(currentUser, origin, dtSince), "plan", deleted);

            BuildList(ProjIntService.GetChanges(currentUser, origin, dtSince), "projectintegration", changes);
            //BuildList(ProjIntService.GetDeleted().Result.Where(p => p.DateUpdated > dtSince), "projectintegration", deleted);

            BuildList(ProjectService.GetChanges(currentUser, origin, dtSince), "project", changes);
            BuildList(ProjectService.GetDeletedSince(currentUser, origin, dtSince), "project", deleted);

            BuildList(SectionService.GetChanges(currentUser, origin, dtSince), "section", changes);
            BuildList(SectionService.GetDeletedSince(currentUser, origin, dtSince), "section", deleted);

            BuildList(UserService.GetChanges(currentUser, origin, dtSince), "user", changes);
            BuildList(UserService.GetDeletedSince(currentUser, origin, dtSince), "user", deleted);

            var ret = new DataChanges() { Id = 1,  Querydate = dtNow, Changes = changes.ToArray(), Deleted = deleted.ToArray() };
            return Ok(ret);
        }
    }
}