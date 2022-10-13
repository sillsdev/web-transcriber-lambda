using System;
using System.Net;
using System.Threading.Tasks;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using Xunit;
using System.Net.Http;
using JsonApiDotNetCore.Resources;

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
            passage1.SectionId = section1.Id;
            passage2.SectionId = section1.Id;
            passage3.SectionId = section2.Id;
            passage4.SectionId = section2.Id;
            await SaveIt(routePrefix, route, passage1);
            Assert.NotEqual(0, passage1.Id);
            await SaveIt(routePrefix, route, passage2);
            Assert.NotEqual(0, passage2.Id);
            await SaveIt(routePrefix, route, passage3);
            Assert.NotEqual(0, passage3.Id);
            await SaveIt(routePrefix, route, passage4);
            Assert.NotEqual(0, passage4.Id);

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
