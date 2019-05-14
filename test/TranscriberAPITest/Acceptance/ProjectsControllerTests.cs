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

namespace TranscriberAPI.Tests
{
    [Collection("WebHostCollection")]
    public class ProjectsControllerTests
    {
        private TestFixture<TestStartup> _fixture;
        private Fakers faker;
        private long myRunNo;
        public ProjectsControllerTests(TestFixture<TestStartup> fixture)
        {
            _fixture = fixture;
            myRunNo = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            faker = new Fakers(myRunNo);
            
        }
        ~ProjectsControllerTests()
        {
            //delete all projects created
            //eh I don't know how to do that :)
        }
        [Fact]
        public async Task Can_Fetch_One_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = faker.Project;
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var tagResponse = _fixture.DeSerializer.Deserialize<Project>(body);
            Assert.NotNull(tagResponse);
            Assert.Equal(project.Id, tagResponse.Id);
            Assert.Equal(project.Name, tagResponse.Name);

            if (_fixture.DeleteTestData)
            {
                //route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }
        [Fact]
        public async Task Can_Fetch_Many_To_One_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = faker.Project;
            context.Projects.Add(project);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}/projecttype";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var ptResponse = _fixture.DeSerializer.Deserialize<ProjectType>(body);
            Assert.NotNull(ptResponse);
            Assert.Equal(project.ProjecttypeId, ptResponse.Id);

            if (_fixture.DeleteTestData)
            {
                route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }
        [Fact]
        public async Task Can_Create_Heirarchy()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = faker.Project;
            var plan = faker.Plan;

            plan.Project = project;
            project.Plans.Add(plan);

            var section = faker.Section;
            section.Plan = plan;
            plan.Sections.Add(section);

            var passage = faker.Passage;
            passage.Sections.Add(section);
            section.Passages.Add(passage);

            var passagesection = new Passagesection
            {
                Passage = passage,
                Section = section
            };
            context.PassageSections.Add(passagesection);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);

            //assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var tagResponse = _fixture.DeSerializer.Deserialize<Project>(body);
            Assert.NotNull(tagResponse);
            Assert.Equal(project.Id, tagResponse.Id);
            Assert.Equal(project.Name, tagResponse.Name);
        }
        [Fact]
        public async Task Can_Create_Heirarchy_withPOST()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = faker.Project;
            var plan = faker.Plan;
            var section = faker.Section;
            var passage = faker.Passage;
            /*
            plan.Project = project;
            project.Plans.Add(plan);

            section.Plan = plan;
            plan.Sections.Add(section);

            passage.Sections.Add(section);
            section.Passages.Add(passage);

            var passagesection = new PassageSection
            {
                Passage = passage,
                Section = section
            };
            Message: Newtonsoft.Json.JsonSerializationException : Self referencing loop detected with type 'SIL.Transcriber.Models.Plan'.Path 'Passage.Sections[0].Plan.Project.Plans'.
        */
            /*
            plan.Project = project;
            //project.Plans.Add(plan);

            section.Plan = plan;
            plan.Sections.Add(section);

            passage.Sections.Add(section);
            section.Passages.Add(passage);

            var passagesection = new PassageSection
            {
                Passage = passage,
                Section = section
            };
            //Self referencing loop detected with type 'SIL.Transcriber.Models.Section'.Path 'Passage.Sections[0].Plan.Sections'.
            */
            /*
            plan.Project = project;
            //project.Plans.Add(plan);

            section.Plan = plan;
            //plan.Sections.Add(section);

            passage.Sections.Add(section);
            //section.Passages.Add(passage);

            var passagesection = new PassageSection
            {
                Passage = passage,
                Section = section
            };
            passage.PassageSections.Add(passagesection);
         Message: Newtonsoft.Json.JsonSerializationException : Self referencing loop detected for property 'Passage' with type 'SIL.Transcriber.Models.Passage'.Path 'PassageSections[0]'.
         */
            var jsonf = new JsonMediaTypeFormatter();
            jsonf.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
            var json = JsonConvert.SerializeObject(project);
            var content = new
            {
                data = new
                {
                    type = "projects",
                    attributes = new
                    {
                        name = project.Name,
                        description = project.Name,
                        language = "eng-US"
                    },
                    relationships = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>() {
                            {"owner", new Dictionary<string, Dictionary<string, string>>() {
                                { "data", new Dictionary<string, string>() {
                                    { "type", "users" },
                                        { "id", _fixture.CurrentUser.Id.ToString() }
                                }}}},
                            {"organization", new Dictionary<string, Dictionary<string, string>>() {
                                { "data", new Dictionary<string, string>() {
                                    { "type", "organizations" },
                                    { "id", project.OrganizationId.ToString() }
                            }}}},
                            {"group", new Dictionary<string, Dictionary<string, string>>() {
                                { "data", new Dictionary<string, string>() {
                                    { "type", "groups" },
                                    { "id", project.GroupId.ToString() }
                            }}}},
                            {"type", new Dictionary<string, Dictionary<string, string>>() {
                                { "data", new Dictionary<string, string>() {
                                    { "type", "application-types" },
                                    { "id", project.ProjecttypeId.ToString() }
                            }}}}
                        }
                }
            };
            plan.Project = project;
            //project.Plans.Add(plan);
            var route = $"/api/projects";
            var p = _fixture.DeSerializer.Deserialize(JsonConvert.SerializeObject(content));
            //this works in postman
            //var response = await _fixture.SendAsync("POST", "/api/projects", content);
            var (response, data) = await _fixture.PostAsync<Project>(route, p);

            section.Plan = plan;
            //plan.Sections.Add(section);

            //passage.Sections.Add(section);
            //section.Passages.Add(passage);
            var passagesection = new Passagesection
            {
                Passage = passage,
                Section = section
            };


            //Message: Newtonsoft.Json.JsonSerializationException : Self referencing loop detected for property 'Passage' with type 'SIL.Transcriber.Models.Passage'.Path 'PassageSections[0]'.
            //passage.PassageSections.Add(passagesection);
            route = $"/api/passagesections";
            //var json = JsonConvert.SerializeObject(passagesection);
            //422 unprocessable
            //var (psresponse, psdata) = await _fixture.PostAsync<PassageSection>(route, passagesection);

            // act
            //var response = await _fixture.Client.PostAsync(route, passagesection, jsonf);
            response = await _fixture.Client.PostAsJsonAsync(route, passagesection);
            route = response.Headers.Location.OriginalString;
            //await context.SaveChangesAsync();

            //route = $"/api/projects/{project.Id}";
            // act
            response = await _fixture.Client.GetAsync(route);

            //assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var tagResponse = _fixture.DeSerializer.Deserialize<Passagesection>(body);
            Assert.NotNull(tagResponse);
            Assert.Equal(passage.Reference, tagResponse.Passage.Reference);
            Assert.Equal(section.Name, tagResponse.Section.Name);
        }
        [Fact]
        public async Task Can_Fetch_One_to_Many_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var project = faker.Project;
            var plan = faker.Plan;
            //context.Projects.Add(project);
            //await context.SaveChangesAsync();

            //plan.ProjectId = project.Id;
            plan.Project = project;
            project.Plans.Add(plan);
            context.Plans.Add(plan);
            await context.SaveChangesAsync();

            var route = $"/api/projects/{project.Id}/plans";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Documents>(body);
            Assert.NotNull(document.Data);

            var responseList = _fixture.DeSerializer.DeserializeList<Plan>(body);
            Assert.NotNull(responseList);
            var planResponse = responseList.FirstOrDefault(a => a.Id == plan.Id);
            Assert.NotNull(planResponse);
            Assert.Equal(plan.Id, planResponse.Id);
            Assert.Equal(project.Id, planResponse.ProjectId);
            Assert.Equal(plan.Name, planResponse.Name);
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
            var project = faker.Project;
            var integration = faker.Integration;
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
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Documents>(body);
            Assert.NotNull(document.Data);

            var responseList = _fixture.DeSerializer.DeserializeList<Plan>(body);
            Assert.NotNull(responseList);
            var integrationResponse = responseList.FirstOrDefault(a => a.Id == integration.Id);
            Assert.NotNull(integrationResponse);
            Assert.Equal(integration.Id, integrationResponse.Id);
            Assert.Equal(integration.Name, integrationResponse.Name);

            if (_fixture.DeleteTestData)
            {
                route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }

        }
    }
}