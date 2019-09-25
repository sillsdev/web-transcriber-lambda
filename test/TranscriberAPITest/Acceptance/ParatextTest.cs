using System.Threading.Tasks;
using SIL.Paratext.Models;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using Xunit;

namespace TranscriberAPI.Tests.Acceptance
{
    [Collection("WebHostCollection")]
    public class ParatextTest : BaseTest<TestStartup>
    {
        public ParatextTest(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }
        
        [Fact]
        public async Task Can_Fetch_Projects()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();

            //act (and assert)
            var route = $"/paratext/projects";
            var response = await _fixture.Client.GetAsync(route);
            AssertOK(response, route);

            var responseList = DeserializeList<ParatextProject>(response).Result;
            Assert.NotNull(responseList);
        }
        [Fact]
        public async Task Can_Fetch_TextBySection()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = _faker.Project;
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var route = $"/api/paratext/{project.Id}/projecttype";
            //act
            var response = await _fixture.Client.GetAsync(route);
            AssertOK(response, route);

            ProjectType fetch = Deserialize<ProjectType>(response).Result;
            Assert.Equal(fetch.Id, project.ProjecttypeId);

            //cleanup
            if (_fixture.DeleteTestData)
            {
                route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }
        }
    }
}
