using System;
using System.Net.Http;
using JsonApiDotNetCore.Serialization;
using Microsoft.AspNetCore.Hosting;
using SIL.Transcriber.Data;
using Microsoft.AspNetCore.TestHost;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using static SIL.Transcriber.Utility.EnvironmentHelpers;
using SIL.Transcriber.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using SIL.Transcriber.Repositories;
using SIL.Transcriber.Services;

namespace TranscriberAPI.Tests
{
    public class TestFixture<TStartup> : IDisposable where TStartup : class
    {
        private readonly Microsoft.AspNetCore.TestHost.TestServer _server;
        private IServiceProvider _services;

        public TestFixture()
        {
            IWebHostBuilder builder = new WebHostBuilder()
                .UseStartup<TStartup>();

            _server = new TestServer(builder);
            _services = _server.Host.Services;
            Client = _server.CreateClient();
            WebClient = new HttpClient();
            //this doesn't get changes made since process started
            //var token = Environment.GetEnvironmentVariable("BEARER_TOKEN");
            string token = Registry.GetValue(@"HKEY_CURRENT_USER\Environment", "BEARER_TOKEN", "DefaultSomething").ToString();
            Client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token); 
            WebClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            Context = GetService<IDbContextResolver>().GetContext() as AppDbContext;
            DeSerializer = GetService<IJsonApiDeSerializer>();
            Serializer = GetService<IJsonApiSerializer>();
            JsonApiContext = GetService<IJsonApiContext>();
            DeleteTestData = GetVarOrDefault("DeleteTestData", "true").Equals("true");
            CurrentUser = getCurrentUser(); 
        }

        public HttpClient Client { get; set; }
        public HttpClient WebClient { get; set; }
        public AppDbContext Context { get; private set; }
        public IJsonApiDeSerializer DeSerializer { get; private set; }
        public IJsonApiSerializer Serializer { get; private set; }
        public IJsonApiContext JsonApiContext { get; private set; }
        public T GetService<T>() => (T)_services.GetService(typeof(T));
        public User CurrentUser { get; private set; }
        public bool DeleteTestData;

        //wrap async call in non async
        private User getCurrentUser()
        {
            Task<User> task1 = CurrentUserAsync();
            Task.WaitAll(task1);
            return task1.Result;
        }
        private async Task<User> CurrentUserAsync()
        {
            string route = $"/api/currentusers?include=organization-memberships,group-memberships";
            HttpResponseMessage response = await Client.GetAsync(route);
            if (response.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string body = await response.Content.ReadAsStringAsync();
                return DeSerializer.Deserialize<User>(body);
            }
            throw new Exception("Unable to get Current User " + response.StatusCode);
        }
        /*
        public void ReloadDbContext()
        {
            Context = new AppDbContext(GetService<DbContextOptions<AppDbContext>>());
        }
        */
        public async Task<(HttpResponseMessage response, T data)> PostAsync<T>(string route, object data)
        {
            return await SendAsync<T>("POST", route, data);
        }

        public async Task<HttpResponseMessage> SendAsync(string method, string route, object data)
        {
            HttpMethod httpMethod = new HttpMethod(method);
            string json = JsonConvert.SerializeObject(data);
            HttpRequestMessage request = new HttpRequestMessage(httpMethod, route)
            {
                Content = new StringContent(json,
                     Encoding.UTF8,
                    "application/vnd.api+json"
               )
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            return await Client.SendAsync(request);
        }
        public async Task<(HttpResponseMessage response, T data)> SendAsync<T>(string method, string route, object data)
        {
            HttpResponseMessage response = await SendAsync(method, route, data);
            string json = await response.Content.ReadAsStringAsync();
            T obj = (T)DeSerializer.Deserialize(json);
            return (response, obj);
        }
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Client.Dispose();
                    WebClient.Dispose();
                    _server.Dispose();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }
}