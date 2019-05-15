using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using Microsoft.EntityFrameworkCore;
using JsonApiDotNetCore.Serialization;
using Newtonsoft.Json;
using Xunit;
using JsonApiDotNetCore.Models;
using TranscriberAPI.Tests.Utilities;
using System.Net.Http.Formatting;

namespace TranscriberAPI.Tests.Acceptance
{
    [Collection("WebHostCollection")]
    public class ProjectsControllerTests : BaseTest<TestStartup>
    {
        public ProjectsControllerTests(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }
        [Fact]
        public async Task Can_Fetch_One_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = _faker.Project;
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            //act (and assert)
            var route = $"/api/projects/{project.Id}";
            var response = await _fixture.Client.GetAsync(route);
            AssertOK(response, route);

            Project fetch = Deserialize<Project>(response).Result;

            //assert 
            Assert.Equal(project.Id, fetch.Id);
            Assert.Equal(project.Name, fetch.Name);

            //cleanup
            if (_fixture.DeleteTestData)
            {
                //route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }
        [Fact]
        public async Task Can_Fetch_Relationship()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = _faker.Project;
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}/projecttype";
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

        [Fact]
        public async Task Can_Fetch_One_to_Many()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = _faker.Project;
            var plan1 = _faker.Plan;
            var plan2 = _faker.Plan;
            //context.Projects.Add(project);
            //await context.SaveChangesAsync();

            //plan.ProjectId = project.Id;
            plan1.Project = project;
            plan2.Project = project;
            project.Plans.Add(plan1);
            project.Plans.Add(plan2);

            context.Plans.Add(plan1);
            context.Plans.Add(plan2);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}/plans";
            // act
            var response = await _fixture.Client.GetAsync(route);
            AssertOK(response, route);

            var responseList = DeserializeList<Plan>(response).Result;

            Assert.NotNull(responseList);
            Assert.Equal(2, responseList.Count);
            var planResponse = responseList.FirstOrDefault(a => a.Id == plan1.Id);
            Assert.NotNull(planResponse);
            Assert.Equal(plan1.Id, planResponse.Id);
            Assert.Equal(project.Id, planResponse.ProjectId);
            Assert.Equal(plan1.Name, planResponse.Name);

            if (_fixture.DeleteTestData)
            {
                route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }

        [Fact]
        public async Task Can_Fetch_Many_To_Many_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = _faker.Project;
            var integration = _faker.Integration;
            var projint = new ProjectIntegration
            {
                Project = project,
                Integration = integration,
                Settings = "{}"
            };
            context.Projectintegrations.Add(projint);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}/integrations";
            // act
            var response = await _fixture.Client.GetAsync(route);
            // assert
            AssertOK(response, route);

            var responseList = DeserializeList<Integration>(response).Result;

            Assert.NotNull(responseList);
            var integrationResponse = responseList.FirstOrDefault(a => a.Id == integration.Id);
            Assert.NotNull(integrationResponse);
            Assert.Equal(integration.Id, integrationResponse.Id);
            Assert.Equal(integration.Name, integrationResponse.Name);

            if (_fixture.DeleteTestData)
            {
                route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
                route = $"/api/integrations/{integration.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }
    }
}