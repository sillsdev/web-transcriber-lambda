using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using SIL.Transcriber.Data;
using Newtonsoft.Json;
using Xunit;
using JsonApiDotNetCore.Models;
using TranscriberAPI.Tests.Utilities;
using JsonApiDotNetCore.Services;
using System.Text;
using System.IO;
using RestSharp;
using Microsoft.Win32;

namespace TranscriberAPI.Tests.Acceptance
{
    public class BaseTest<TStartup> : IDisposable where TStartup : class
    {
        protected TestFixture<TStartup> _fixture;
        protected AppDbContext _context;
        protected IJsonApiContext _jsonApiContext;
        protected Fakers _faker;
        protected string _myRunNo;

        public BaseTest(TestFixture<TStartup> fixture)
        {
            _fixture = fixture;
            _context = fixture.GetService<AppDbContext>();
            _jsonApiContext = fixture.GetService<IJsonApiContext>();
            _myRunNo = DateTime.Now.TimeOfDay.ToString().Replace(":", "");
            _faker = new Fakers(_myRunNo);
        }
        public void AssertOK(HttpResponseMessage response, string route)
        { 
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code");
        }
       #region HTTP Request Helpers

        public async Task<HttpResponseMessage> Get(string url, string organizationId = "", bool addOrgHeader = false, bool allOrgs = false)
        {
            var httpMethod = new HttpMethod("GET");
            var request = new HttpRequestMessage(httpMethod, url);

            return await MakeRequest(request, organizationId, addOrgHeader, allOrgs);
        }

        public async Task<HttpResponseMessage> Patch(string url, object content, string organizationId = "", bool addOrgHeader = false, bool allOrgs = false)
        {
            var httpMethod = new HttpMethod("PATCH");
            var request = new HttpRequestMessage(httpMethod, url)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(content),
                    Encoding.UTF8,
                    "application/vnd.api+json"
                )
            };

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

            return await MakeRequest(request, organizationId, addOrgHeader, allOrgs);
        }

        public async Task<HttpResponseMessage> Delete(string url)
        {
            var httpMethod = new HttpMethod("DELETE");
            var request = new HttpRequestMessage(httpMethod, url);
            return await MakeRequest(request, "", false);
        }

        public async Task<HttpResponseMessage> PostFormFile(string url, string filePath, object entity)
        {

            var json = JsonConvert.SerializeObject(entity);
            HttpContent stringContent = new StringContent(json, Encoding.UTF8);
            var stream = new FileStream(filePath, FileMode.Open);
            HttpContent fileStreamContent = new StreamContent(stream);
            fileStreamContent.Headers.Add("Content-Type", "audio/mpeg");
            var formData = new MultipartFormDataContent
            {
                {stringContent, "jsonString" },
                {fileStreamContent, "file", filePath }
            };
            
            // add files to upload (works with compatible verbs)
            var response = await _fixture.Client.PostAsync(url, formData);
            return response;
        }

        public async Task<HttpResponseMessage> PostAsJson(string url, object content, string organizationId = "", bool addOrgHeader = false, bool allOrgs = false)
        {
            var httpMethod = new HttpMethod("POST");
            var serializedContent = JsonConvert.SerializeObject(content);
            var request = new HttpRequestMessage(httpMethod, url)
            {
                Content = new StringContent(
                    serializedContent,
                    Encoding.UTF8,
                    "application/json"
                )
            };

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            return await MakeRequest(request, organizationId, false, allOrgs);
        }
        public async Task<HttpResponseMessage> Post(string url, object content, string organizationId = "", bool addOrgHeader = false, bool allOrgs = false)
        {
            var httpMethod = new HttpMethod("POST");
            var serializedContent = JsonConvert.SerializeObject(content);
            var request = new HttpRequestMessage(httpMethod, url)
            {
                Content = new StringContent(
                    serializedContent,
                    Encoding.UTF8,
                    "application/vnd.api+json"
                )
            };

            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");

            return await MakeRequest(request, organizationId, false, allOrgs);
        }

        public object ResourcePatchPayload(
            string type,
            object id,
            Dictionary<string, object> attributes,
            Dictionary<string, object> relationships = null)
        {
            return new
            {
                data = new
                {
                    id,
                    type,
                    attributes,
                    relationships = relationships ?? new Dictionary<string, object>()
                }
            };
        }

        public async Task<HttpResponseMessage> MakeRequest(HttpRequestMessage request, string organizationId = "", bool addOrgHeader = true, bool allOrgs = false)
        {
            if (addOrgHeader)
            {
                // NOTE: HttpRequestMessage omits headers if set to empty string...
                request.Headers.Add("Organization", allOrgs ? "-" : organizationId);
            }

            var response = await _fixture.Client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                var body = await response.Content.ReadAsStringAsync();
                Console.Write(body);

                throw new Exception("500 errors must not exist");
            }

            return response;
        }

        #endregion

        #region Deserialization Helpers

        public async Task<ICollection<T>> DeserializeList<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            var deserializedBody = _fixture
              .DeSerializer
              .DeserializeList<T>(body);

            return deserializedBody;
        }

        public async Task<T> Deserialize<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            var deserializedBody = _fixture
              .DeSerializer
              .Deserialize<T>(body);

            return deserializedBody;
        }

        public async Task<Documents> DeserializeDocumentList(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            var deserializedBody = JsonConvert.DeserializeObject<Documents>(body);

            return deserializedBody;
        }


        public async Task<Document> DeserializeDocument(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();

            var deserializedBody = JsonConvert.DeserializeObject<Document>(body);

            return deserializedBody;
        }

        #endregion

        public void Dispose()
        {
            //ack! _fixture.Context.Database.EnsureDeleted();
        }
    }
}
