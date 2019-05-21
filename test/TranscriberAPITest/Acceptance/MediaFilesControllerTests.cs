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
            Assert.True(HttpStatusCode.NoContent == response.StatusCode, $"{route} returned {response.StatusCode} status code");
        }

        public IFormFile AsMockIFormFile(FileInfo physicalFile)
        {
            var fileMock = new Mock<IFormFile>();
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms);
            writer.Write(physicalFile.OpenRead());
            writer.Flush();
            ms.Position = 0;
            var fileName = physicalFile.Name;
            //Setup mock file using info from physical file
            fileMock.Setup(_ => _.FileName).Returns(fileName);
            fileMock.Setup(_ => _.Length).Returns(ms.Length);
            fileMock.Setup(m => m.OpenReadStream()).Returns(ms);
            fileMock.Setup(m => m.ContentDisposition).Returns(string.Format("inline; filename={0}", fileName));
            //...setup other members (code removed for brevity)


            return fileMock.Object;
        }
        [Fact]
        public async Task RegularPostFails()
        {
            var media = _faker.Mediafile;
            media.PassageId = 1;

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
        [Fact]
        public async Task CreateOneWithoutPassage()
        {
            var context = _fixture.GetService<AppDbContext>();
            var media = _faker.Mediafile;
            media.PassageId = null;  //not 0!

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

    }
}
