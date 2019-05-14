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

namespace TranscriberAPI.Tests.Acceptance
{
    [Collection("WebHostCollection")]
    public class CreateNewTest : BaseTest<TestStartup>
    {
        public CreateNewTest(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }
        private async Task SaveIt(string routePrefix, string route, IIdentifiable obj)
        {
            var response = await PostAsJson(routePrefix + route, obj);
            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{routePrefix + route} returned {response.StatusCode} status code");
            var newroute = response.Headers.Location.OriginalString;
            obj.StringId = newroute.Substring(newroute.LastIndexOf("/")+1);
        }
        [Fact]
        public async Task CreateHierarchyLocal()
        {
            await CreateHierarchy();
        }
        [Fact]
        public async Task CreateHierarchyLambdaDev()
        {
            await CreateHierarchy("https://9u6wlhwuha.execute-api.us-east-2.amazonaws.com/dev");
        }

        private async Task CreateHierarchy(string routePrefix = "")
        {
            var context = _fixture.GetService<AppDbContext>();
            var project = _faker.Project;
            var plan = _faker.Plan;
            var section1 = _faker.Section;
            var section2 = _faker.Section;
            var passage1 = _faker.Passage;
            var passage2 = _faker.Passage;
            var passage3 = _faker.Passage;
            var passage4 = _faker.Passage;
            passage2.Sequencenum = 2;
            passage4.Sequencenum = 3;
            section2.Sequencenum = 2;

            await SaveIt(routePrefix, $"/api/projects", project);
            Assert.NotEqual(0, project.Id);

            plan.ProjectId = project.Id;
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

            var ps1 = new Passagesection
            {
                PassageId = passage1.Id,
                SectionId = section1.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps1);
            Assert.NotEqual(0, ps1.Id);
            var ps2 = new Passagesection
            {
                PassageId = 0,
                SectionId = 0
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps2);
            Assert.NotEqual(0, ps2.Id);
            var ps3 = new Passagesection
            {
                PassageId = passage3.Id,
                SectionId = section2.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps3);
            Assert.NotEqual(0, ps3.Id);
            var ps4 = new Passagesection
            {
                PassageId = passage4.Id,
                SectionId = section2.Id
            };
            await SaveIt(routePrefix, $"/api/passagesections", ps4);
            Assert.NotEqual(0, ps4.Id);

        }
    }
}
