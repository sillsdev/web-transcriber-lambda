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

namespace TranscriberAPI.Tests.Acceptance
{
    [Collection("WebHostCollection")]
    public class UsersControllerTests : BaseTest<TestStartup>
    {
        public UsersControllerTests(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }
        
        [Fact]
        public async Task Can_Get_Current_User()
        {
            var context = _fixture.GetService<AppDbContext>();
            var route = $"/api/currentusers";
            // act
            var response = await _fixture.Client.GetAsync(route);
            AssertOK(response, route);

            User fetch = Deserialize<User>(response).Result;
            Assert.NotNull(fetch);
            Assert.Equal(_fixture.CurrentUser.Id, fetch.Id);

            //DO NOT DELETE!!
        }
        [Fact]
        public async Task Can_Get_Users()
        {
            var context = _fixture.GetService<AppDbContext>();
            var route = $"/api/users";
            // act
            var response = await _fixture.Client.GetAsync(route);
            AssertOK(response, route);
            ICollection<User> responseList = DeserializeList<User>(response).Result;
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
            AssertOK(response, route);

            User userResponse = Deserialize<User>(response).Result;
            Assert.NotNull(userResponse);
            Assert.Equal(_fixture.CurrentUser.Id, userResponse.Id);

            //DO NOT DELETE!!
        }
        [Fact]
        public async Task Can_Fetch_One_Through_Id_If_InMyOrg()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = _faker.User;
            var myOrgMem = _fixture.CurrentUser.OrganizationMemberships.First<OrganizationMembership>();
            var orgmem = new OrganizationMembership
            {
                User = user,
                OrganizationId = myOrgMem.OrganizationId
            };
            context.Organizationmemberships.Add(orgmem);
            await context.SaveChangesAsync();

            var route = $"/api/users/{user.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);
            // assert
            AssertOK(response, route);

            User userResponse = Deserialize<User>(response).Result;
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
            var user = _faker.User;
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var route = $"/api/users/{user.Id}";
            // act
            var response = await _fixture.Client.GetAsync(route);
            // assert
            Assert.True(HttpStatusCode.NotFound == response.StatusCode, $"{route} returned {response.StatusCode}");

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
            var user = _faker.User;
            var org = _faker.Organization;
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
            AssertOK(response, route);

            ICollection<OrganizationMembership> responseList = DeserializeList<OrganizationMembership>(response).Result;
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
            var user = _faker.User;
            var project = _faker.Project;
            var projuser = new ProjectUser
            {
                User = user,
                Project = project,
                RoleId = 2
            };
            context.Projectusers.Add(projuser);
            await context.SaveChangesAsync();

            var route = $"/api/Users/{user.Id}/projects";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            AssertOK(response, route);

            ICollection<Project> responseList = DeserializeList<Project>(response).Result;

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
        /* No organizations link YET
        [Fact]
        public async Task Can_Fetch_Many_To_Many_Through_Id2()
        {
            // arrange
            var context = _fixture.GetService<AppDbContext>();
            var user = _faker.User;
            var org = _faker.Organization;
            var myOrgMem = _fixture.CurrentUser.OrganizationMemberships.First<OrganizationMembership>();

            var orgmem = new OrganizationMembership
            {
                User = user,
                Organization = org
            };
            context.Organizationmemberships.Add(orgmem);
            context.Organizationmemberships.Add(new OrganizationMembership
            {
                User = user,
                OrganizationId = myOrgMem.OrganizationId
            });
            await context.SaveChangesAsync();

            var route = $"/api/Users/{user.Id}/organizations";
            // act
            var response = await _fixture.Client.GetAsync(route);

            // assert
            AssertOK(response, route);

            ICollection<Organization> responseList = DeserializeList<Organization>(response).Result;

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
        */
    }
}