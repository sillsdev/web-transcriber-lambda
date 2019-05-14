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
        private async Task SaveIt(string route, IIdentifiable obj)
        {
            var response = await PostAsJson(route, obj);
            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            var newroute = response.Headers.Location.OriginalString;
            obj.StringId = newroute.Substring(newroute.LastIndexOf("/")+1);
        }
        [Fact]
        public async Task CreateHierarchy()
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

            await SaveIt($"/api/projects", project);
            Assert.NotEqual(0, project.Id);

            plan.ProjectId = project.Id;
            await SaveIt($"/api/plans", plan);
            Assert.NotEqual(0, plan.Id);

            section1.PlanId = plan.Id;
            section2.PlanId = plan.Id;
            await SaveIt($"/api/sections", section1);
            Assert.NotEqual(0, section1.Id);
            await SaveIt($"/api/sections", section2);
            Assert.NotEqual(0, section2.Id);

            var route = $"/api/passages";
            //assert
            await SaveIt(route, passage1);
            Assert.NotEqual(0, passage1.Id);
            await SaveIt(route, passage2);
            Assert.NotEqual(0, passage2.Id);
            await SaveIt(route, passage3);
            Assert.NotEqual(0, passage3.Id);
            await SaveIt(route, passage4);
            Assert.NotEqual(0, passage4.Id);

            var ps = new PassageSection
            {
                PassageId = passage1.Id,
                SectionId = section1.Id
            };
            await SaveIt($"/api/passagesections", ps);
            Assert.NotEqual(0, ps.Id);
            
        }
    }
}
