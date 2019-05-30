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
    public class PassagesControllerTests : BaseTest<TestStartup>
    {
        public PassagesControllerTests(TestFixture<TestStartup> fixture) :base(fixture)
        {
        }

        [Fact]
        public async Task CreateOneTheComplicatedWay()
        {
            var context = _fixture.GetService<AppDbContext>();
            var section = _faker.Section;
            var passage = _faker.Passage;
            var route = $"/api/passages";
            //no id 
            var content = new
            {
                data = new
                {
                    type = "passages",
                    attributes =  new 
                    {
                        sequencenum = passage.Sequencenum,
                        book =  passage.Book,
                        reference = passage.Reference
                    },
                    relationships = new
                    {
                        sections = new
                        {
                            data = new
                            {
                                type = "sections",
                                id = 1
                            }
                        }
                    }
                }
            };
            //null object error
            //var objjson = _fixture.Serializer.Serialize(passage);
            //var contjson = JsonConvert.SerializeObject(content);
            //assert
            var response = await Post(route, content);
            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            var newpassage = await Deserialize<Passage>(response);
            Assert.Equal(passage.Book, newpassage.Book);
        }
        [Fact]
        public async Task CreateOneWithJson()
        {
            var context = _fixture.GetService<AppDbContext>();
            var section = _faker.Section;
            var passage = _faker.Passage;
            var route = $"/api/passages";
            //assert
            var response = await PostAsJson(route, passage);
            Assert.True(HttpStatusCode.Created == response.StatusCode, $"{route} returned {response.StatusCode} status code");
            route = response.Headers.Location.OriginalString;

            response = await _fixture.Client.GetAsync(route);

            //assert
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(HttpStatusCode.OK == response.StatusCode, $"{route} returned {response.StatusCode} status code with payload: {body}");

            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.NotNull(document.Data);

            var tagResponse = _fixture.DeSerializer.Deserialize<Passage>(body);
            Assert.NotNull(tagResponse);
            Assert.Equal(passage.Reference, tagResponse.Reference);
        }
    }
}
