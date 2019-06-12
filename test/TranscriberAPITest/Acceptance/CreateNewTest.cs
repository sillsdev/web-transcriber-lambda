using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using Newtonsoft.Json;
using Xunit;
using JsonApiDotNetCore.Models;
using TranscriberAPI.Tests.Utilities;
using System.Net.Http;

namespace TranscriberAPI.Tests.Acceptance
{
    [Collection("WebHostCollection")]
    public class CreateNewTest : BaseTest<TestStartup>
    {
        public CreateNewTest(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }
        protected async Task SaveIt(string routePrefix, string route, IIdentifiable obj)
        {
            var response = await PostAsJson(routePrefix + route, obj);
            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{routePrefix + route} returned {response.StatusCode} status code");
            var newroute = response.Headers.Location.OriginalString;
            obj.StringId = newroute.Substring(newroute.LastIndexOf("/") + 1);
        }
        [Fact]
        public async Task CreateHierarchyLocal()
        {
            await CreateHierarchy();
        }
        [Fact]
        public async Task CreateHierarchyLambdaDev()
        {
            HttpClient testclient = _fixture.Client;
            _fixture.Client = _fixture.WebClient;
            try
            {
                await CreateHierarchy("https://9u6wlhwuha.execute-api.us-east-2.amazonaws.com/dev");
            }
            finally 
            {

                _fixture.Client = testclient;
            }
        }

        private async Task CreateHierarchy(string routePrefix = "")
        {
            var context = _fixture.GetService<AppDbContext>();
            var org = _faker.Organization;
            var group = _faker.Group;
            var user = _faker.User;
            var integration = _faker.Integration;
            var projtype = _faker.ProjectType;
            var project = _faker.Project;
            var plantype = _faker.PlanType;
            var plan = _faker.Plan;
            var section1 = _faker.Section;
            var section2 = _faker.Section;
            var passage1 = _faker.Passage;
            var passage2 = _faker.Passage;
            var passage3 = _faker.Passage;
            var passage4 = _faker.Passage;
            var media = _faker.Mediafile;
            passage2.Sequencenum = 2;
            passage4.Sequencenum = 3;
            section2.Sequencenum = 2;

            await SaveIt(routePrefix, $"/api/organizations", org);
            group.OwnerId = org.Id;
            await SaveIt(routePrefix, $"/api/groups", group);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var om = new OrganizationMembership()
            {
                UserId = user.Id,
                OrganizationId = org.Id
            };
            await SaveIt(routePrefix, $"/api/organizationmemberships", om);
            var gm = new GroupMembership()
            {
                UserId = user.Id,
                GroupId = group.Id,
                RoleId = 2,
                FontSize = "10"
            };
            await SaveIt(routePrefix, $"/api/groupmemberships", gm);
            var gm2 = new GroupMembership()
            {
                UserId = _fixture.CurrentUser.Id,
                GroupId = group.Id,
                RoleId = 2,
                FontSize = "10"
            };
            await SaveIt(routePrefix, $"/api/groupmemberships", gm2);

            var ur = new UserRole()
            {
                UserId = user.Id,
                OrganizationId = org.Id,
                RoleId = 2
            };
            await SaveIt(routePrefix, $"/api/userroles", ur);

            await SaveIt(routePrefix, $"/api/projecttypes", projtype);
            project.ProjecttypeId = projtype.Id;
            project.OrganizationId = org.Id;
            project.GroupId = group.Id;
            project.OwnerId = user.Id;
            await SaveIt(routePrefix, $"/api/projects", project);
            Assert.NotEqual(0, project.Id);

            await SaveIt(routePrefix, $"/api/integrations", integration);
            Assert.NotEqual(0, integration.Id);

            var projint = new ProjectIntegration()
            {
                ProjectId = project.Id,
                IntegrationId = integration.Id,
            };
            await SaveIt(routePrefix, $"/api/projectintegrations", projint);
            Assert.NotEqual(0, projint.Id);

            plan.ProjectId = project.Id;

            await SaveIt(routePrefix, $"/api/plantypes", plantype);
            plan.PlantypeId = plantype.Id;

            await SaveIt(routePrefix, $"/api/plans", plan);
            Assert.NotEqual(0, plan.Id);

            section1.PlanId = plan.Id;
            section2.PlanId = plan.Id;
            await SaveIt(routePrefix, $"/api/sections", section1);
            Assert.NotEqual(0, section1.Id);
            await SaveIt(routePrefix, $"/api/sections", section2);
            Assert.NotEqual(0, section2.Id);

            var route = $"/api/passages";
            //assert
            await SaveIt(routePrefix, route, passage1);
            Assert.NotEqual(0, passage1.Id);
            await SaveIt(routePrefix, route, passage2);
            Assert.NotEqual(0, passage2.Id);
            await SaveIt(routePrefix, route, passage3);
            Assert.NotEqual(0, passage3.Id);
            await SaveIt(routePrefix, route, passage4);
            Assert.NotEqual(0, passage4.Id);

            var ps1 = new PassageSection
            {
                PassageId = passage1.Id,
                SectionId = section1.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps1);
            Assert.NotEqual(0, ps1.Id);
            var ps2 = new PassageSection
            {
                PassageId = passage2.Id,
                SectionId = section1.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps2);
            Assert.NotEqual(0, ps2.Id);
            var ps3 = new PassageSection
            {
                PassageId = passage3.Id,
                SectionId = section2.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps3);
            Assert.NotEqual(0, ps3.Id);
            var ps4 = new PassageSection
            {
                PassageId = passage4.Id,
                SectionId = section2.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps4);
            Assert.NotEqual(0, ps4.Id);

            var up1 = new UserPassage()
            {
                UserId = user.Id,
                PassageId = passage1.Id,
                ActivityName = "TestActivityState"
            };
            await SaveIt(routePrefix, $"/api/userpassages", up1);
            var up2 = new UserPassage()
            {
                UserId = user.Id,
                PassageId = passage2.Id,
                ActivityName ="TestActivityState"
            };
            await SaveIt(routePrefix, $"/api/userpassages", up2);
            var up3 = new UserPassage()
            {
                UserId = user.Id,
                PassageId = passage3.Id,
                ActivityName = "Ready for Transcription"
            };
            await SaveIt(routePrefix, $"/api/userpassages", up3);
            var up4 = new UserPassage()
            {
                UserId = user.Id,
                PassageId = passage4.Id,
                ActivityName = "Ready for Transcription"
            };
            await SaveIt(routePrefix, $"/api/userpassages", up4);
            media.PassageId = passage1.Id;
            await SaveIt(routePrefix, $"/api/mediafiles", media);

            if (_fixture.DeleteTestData)
            {
                route = routePrefix + $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }
    }
}
