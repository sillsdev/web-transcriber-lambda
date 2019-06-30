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
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.AspNetCore.Http;
using System.IO;
using Moq;

namespace TranscriberAPI.Tests.Acceptance
{
    [Collection("WebHostCollection")]
    public class MediafilesControllerTests : BaseTest<TestStartup>
    {
        public MediafilesControllerTests(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }
        [Fact]
        public async Task MustHaveFileToCreate()
        {
            var media = _faker.Mediafile;
            var route = $"/api/mediafiles";
            var response = await PostAsJson(route, media);
            Assert.True(HttpStatusCode.NotImplemented == response.StatusCode, $"{route} returned {response.StatusCode} status code");
        }

        [Fact]
        public async Task CreateOneWithPassage()
        {
            var context = _fixture.GetService<AppDbContext>();
            var media = _faker.Mediafile;
            /*            var plan = _faker.Plan;
                        var section = _faker.Section;
                        var passage = _faker.Passage;

                        plan.ProjectId = 5;
                        section.Plan = plan;
                        //section.Passages.Add(passage);
                        passage.Sections.Add(section);

                        context.Passages.Add(passage);
                        await context.SaveChangesAsync();

                        media.PassageId = passage.Id;
            */
            media.PassageId =1;
            
            var route = $"/api/mediafiles/file";
            var response = await PostFormFile(route, "mpthreetest.mp3", media);

                Assert.True(HttpStatusCode.Created == response.StatusCode, $"{route} returned {response.StatusCode} status code");
                route = response.Headers.Location.OriginalString;

                response = await _fixture.Client.GetAsync(route);

                //assert
                var body = await response.Content.ReadAsStringAsync();
                Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

                var document = JsonConvert.DeserializeObject<Document>(body);
                Assert.NotNull(document.Data);

                var tagResponse = _fixture.DeSerializer.Deserialize<Mediafile>(body);
                Assert.NotNull(tagResponse);
                Assert.Equal(media.Transcription, tagResponse.Transcription);

        }
        private async Task<Mediafile> GetNewMedia(Mediafile media, string filename)
        {
            var route = $"/api/mediafiles/file";
            var response = await PostFormFile(route, filename, media);

            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            route = response.Headers.Location.OriginalString;

            response = await _fixture.Client.GetAsync(route);

            //assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            return _fixture.DeSerializer.Deserialize<Mediafile>(body);
        }
        private static void UploadObject(string url, string filePath)
        {
            Console.WriteLine(url);
            HttpWebRequest httpRequest = WebRequest.Create(url) as HttpWebRequest;
            httpRequest.Method = "PUT";
            using (Stream dataStream = httpRequest.GetRequestStream())
            {
                var buffer = new byte[8000];
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    int bytesRead = 0;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        dataStream.Write(buffer, 0, bytesRead);
                    }
                }
            }
            try
            {
                HttpWebResponse response = httpRequest.GetResponse() as HttpWebResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
 
        [Fact]
        public async Task<int> CreateOneWithSignedURL()
        {
            var route = $"/api/mediafiles";
            var file = "NIV11-LUK-001-026038v01.mp3";
            var media = _faker.Mediafile;
            media.PassageId = null;  //not 0!
            media.OriginalFile = file;
            media.ContentType = "audit/mpeg";

            var response = await PostAsJson($"/api/mediafiles", media);
            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            route = response.Headers.Location.OriginalString;

            response = await _fixture.Client.GetAsync(route);

            //assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var tagResponse = _fixture.DeSerializer.Deserialize<Mediafile>(body);
            Assert.NotNull(tagResponse);

            //have our signed url now so post the file
            UploadObject(tagResponse.AudioUrl, "C:\\Users\\Sara Hentzel\\Music\\" + tagResponse.OriginalFile);
            return tagResponse.Id;
        }
        [Fact]
        public async Task GetOneWithSignedURL()
        {
            int id = await CreateOneWithSignedURL();
            var route = $"/api/mediafiles/" + id.ToString() + "/fileurl";
            
            var response = await _fixture.Client.GetAsync(route);

            //assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var tagResponse = _fixture.DeSerializer.Deserialize<Mediafile>(body);
            Assert.NotNull(tagResponse);

            //our get url is in the audiourl
            route = tagResponse.AudioUrl;
            var auth = _fixture.WebClient.DefaultRequestHeaders.GetValues("Authorization");
            _fixture.WebClient.DefaultRequestHeaders.Remove("Authorization");

            response = await _fixture.WebClient.GetAsync(route); //use webclient so it doesn't mess with the route;
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            _fixture.WebClient.DefaultRequestHeaders.Add("Authorization", auth);

            var fileStream = new FileStream("myfile.mp3", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
            fileStream.Close();
        }
        [Fact]
        public async Task CreateOneWithoutPassage()
        {
            var media = _faker.Mediafile;
            media.PassageId = null;  //not 0!

            var tagResponse = await GetNewMedia(media, "mpthreetest.mp3");
            Assert.NotNull(tagResponse);
            Assert.Equal(media.Transcription, tagResponse.Transcription);

        }
        [Fact]
        public async Task CanGetAudioFile()
        {
            var media = _faker.Mediafile;
            media.PassageId = 1;
            //save it
            media = await GetNewMedia(media, "mpthreetest.mp3");

            var route = $"/api/mediafiles/" + media.Id.ToString() + "/file";

            var response = await _fixture.Client.GetAsync(route);
            //assert
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            Assert.Equal(media.Filesize, response.Content.Headers.ContentLength);
            var fileStream = new FileStream("myfile.mp3", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fileStream);
            fileStream.Close();

        }
        [Fact]
        public async Task CanDelete()
        {
            var media = _faker.Mediafile;
            //save it
            media = await GetNewMedia(media, "mpthreetest.mp3");

            //verify file exists on s3
            var route = $"/api/s3files/Plan" + media.PlanId.ToString() + "/" + media.AudioUrl;
            var response = await _fixture.Client.GetAsync(route);
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code");

            route = $"/api/mediafiles/" + media.Id.ToString();
            response = await _fixture.Client.DeleteAsync(route);
            //assert
            Assert.True(HttpStatusCode.NoContent == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            //assert audiofile has been deleted
            route = $"/api/s3files/Plan" + media.PlanId.ToString() + "/" + media.AudioUrl;
            response = await _fixture.Client.GetAsync(route);
            Assert.True(HttpStatusCode.NotFound == response.StatusCode, $"{route} returned {response.StatusCode} status code");
        }
    }
}
