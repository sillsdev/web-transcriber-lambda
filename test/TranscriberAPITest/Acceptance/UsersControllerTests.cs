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

namespace TranscriberAPI.Tests
{
    [Collection("WebHostCollection")]
    public class UsersControllerTests
    {
        private TestFixture<TestStartup> _fixture;
        private Fakers faker;
        private long myRunNo;
        public UsersControllerTests(TestFixture<TestStartup> fixture)
        {
            _fixture = fixture;
            myRunNo = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            faker = new Fakers(myRunNo);

        }
        
        [Fact]
        public async Task Can_Get_Current_User()
        {
            var context = _fixture.GetService<AppDbContext>();
            var route = $"/api/currentusers";
            // act
            var response = await _fixture.Client.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);
            var userResponse = _fixture.DeSerializer.Deserialize<User>(body);
            Assert.NotNull(userResponse);
            Assert.Equal(_fixture.CurrentUser.Id, userResponse.Id);

            //DO NOT DELETE!!
        }
        [Fact]
        public async Task Can_Get_Users()
        {
            var context = _fixture.GetService<AppDbContext>();
            var route = $"/api/users";
            // act
            var response = await _fixture.Client.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Documents>(body);
            Assert.NotNull(document.Data);

            var responseList = _fixture.DeSerializer.DeserializeList<User>(body);
            Assert.NotNull(responseList);
            var oResponse = responseList.FirstOrDefault(a => a.Id == _fixture.CurrentUser.Id);
            Assert.NotNull(oResponse);
            Assert.Equal(_fixture.CurrentUser.Id, oResponse.Id);

            //DO NOT DELETE!!
        }
        [Fact]
        public async Task Can_Get_Current_User_Through_Id()
        {
            var context = _fixture.GetService<AppDbContext>();
            var route = $"/api/users/{_fixture.CurrentUser.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);
            var userResponse = _fixture.DeSerializer.Deserialize<User>(body);
            Assert.NotNull(userResponse);
            Assert.Equal(_fixture.CurrentUser.Id, userResponse.Id);

            //DO NOT DELETE!!
        }
        [Fact]
        public async Task Can_Fetch_One_Through_Id_If_InMyOrg()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = faker.User;
            var myOrgMem = _fixture.CurrentUser.OrganizationMemberships.First<OrganizationMembership>();
            var orgmem = new OrganizationMembership
            {
                User = user,
                Organization = myOrgMem.Organization
            };
            context.Organizationmemberships.Add(orgmem);
            await context.SaveChangesAsync();

            var route = $"/api/users/{user.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var userResponse = _fixture.DeSerializer.Deserialize<User>(body);
            Assert.NotNull(userResponse);
            Assert.Equal(user.Id, userResponse.Id);
            Assert.Equal(user.Name, userResponse.Name);

            if (_fixture.DeleteTestData)
            {
                //route = $"/api/users/{user.Id}";
                await _fixture.Client.DeleteAsync(route);
            }
        }
        [Fact]
        public async Task Cant_Fetch_One_Through_Id_If_NotInMyOrg()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = faker.User;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var route = $"/api/users/{user.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.NotFound == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            if (_fixture.DeleteTestData)
            {
                //route = $"/api/users/{user.Id}";
                await _fixture.Client.DeleteAsync(route);
            }
        }
        [Fact]
        public async Task Can_Fetch_One_to_Many_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = faker.User;
            var org = faker.Organization;
            var orgmem = new OrganizationMembership
            {
                User = user,
                Organization = org
            };
            context.Organizationmemberships.Add(orgmem);
            await context.SaveChangesAsync();

            var route = $"/api/Users/{user.Id}/organization-memberships";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Documents>(body);
            Assert.NotNull(document.Data);

            var responseList = _fixture.DeSerializer.DeserializeList<OrganizationMembership>(body);
            Assert.NotNull(responseList);
            var oResponse = responseList.FirstOrDefault(a => a.Id == orgmem.Id);
            Assert.NotNull(oResponse);
            Assert.Equal(orgmem.Id, oResponse.Id);
            Assert.Equal(user.Id, oResponse.UserId);
            Assert.Equal(org.Id, oResponse.OrganizationId);
            if (_fixture.DeleteTestData)
            {
                route = $"/api/users/{user.Id}";
                await _fixture.Client.DeleteAsync(route);
                route = $"/api/organizations/{org.Id}";
                await _fixture.Client.DeleteAsync(route);
            }
        }

        [Fact]
        public async Task Can_Fetch_Many_To_Many_Through_Id()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = faker.User;
            var project = faker.Project;
            var projuser = new ProjectUser
            {
                User = user,
                Project = project
            };
            context.Projectusers.Add(projuser);
            await context.SaveChangesAsync();

            var route = $"/api/Users/{user.Id}/projects";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Documents>(body);
            Assert.NotNull(document.Data);

            var responseList = _fixture.DeSerializer.DeserializeList<Project>(body);
            Assert.NotNull(responseList);
            var oResponse = responseList.FirstOrDefault(a => a.Id == project.Id);
            Assert.NotNull(oResponse);
            Assert.Equal(project.Id, oResponse.Id);
            Assert.Equal(project.Name, oResponse.Name);

            if (_fixture.DeleteTestData)
            {
                route = $"/api/users/{user.Id}";
                await _fixture.Client.DeleteAsync(route);
                route = $"/api/projects/{project.Id}";
                await _fixture.Client.DeleteAsync(route);
            }
        }
        [Fact]
        public async Task Can_Fetch_Many_To_Many_Through_Id2()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = faker.User;
            var org = faker.Organization;
            var orgmem = new OrganizationMembership
            {
                User = user,
                Organization = org
            };
            context.Organizationmemberships.Add(orgmem);
            await context.SaveChangesAsync();

            var route = $"/api/Users/{user.Id}/organizations";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Documents>(body);
            Assert.NotNull(document.Data);

            var responseList = _fixture.DeSerializer.DeserializeList<Organization>(body);
            Assert.NotNull(responseList);
            var oResponse = responseList.FirstOrDefault(a => a.Id == org.Id);
            Assert.NotNull(oResponse);
            Assert.Equal(org.Id, oResponse.Id);


            if (_fixture.DeleteTestData)
            {
                route = $"/api/users/{user.Id}";
                await _fixture.Client.DeleteAsync(route);
                route = $"/api/organizations/{org.Id}";
                await _fixture.Client.DeleteAsync(route);
            }
        }
}
}