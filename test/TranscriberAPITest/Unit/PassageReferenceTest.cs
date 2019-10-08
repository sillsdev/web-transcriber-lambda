using System.Threading.Tasks;
using SIL.Transcriber.Models;
using SIL.Transcriber.Data;
using Xunit;
using TranscriberAPI.Tests.Acceptance;

namespace TranscriberAPI.Tests.Unit
{
    [Collection("WebHostCollection")]
    public class PassageReferenceTest : BaseTest<TestStartup>
    {
        public PassageReferenceTest(TestFixture<TestStartup> fixture) : base(fixture)
        {
        }

        [Fact]
        public void ReferenceOneChapterTwoVerses()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "4:2-3";

            Assert.Equal(4, passage.StartChapter);
            Assert.Equal(4, passage.EndChapter);
            Assert.Equal(2, passage.StartVerse);
            Assert.Equal(3, passage.EndVerse);
        }
        [Fact]
        public void ReferenceOneChapterOneVerse()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "1:2";

            Assert.Equal(1, passage.StartChapter);
            Assert.Equal(1, passage.EndChapter);
            Assert.Equal(2, passage.StartVerse);
            Assert.Equal(2, passage.EndVerse);
        }
        [Fact]
        public void ReferenceNoChapterTwoVerses()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "2-4";

            Assert.Equal(1, passage.StartChapter);
            Assert.Equal(1, passage.EndChapter);
            Assert.Equal(2, passage.StartVerse);
            Assert.Equal(4, passage.EndVerse);
        }
        [Fact]
        public void ReferenceNoChapterOneVerse()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "1";

            Assert.Equal(1, passage.StartChapter);
            Assert.Equal(1, passage.EndChapter);
            Assert.Equal(1, passage.StartVerse);
            Assert.Equal(1, passage.EndVerse);
        }
        [Fact]
        public void ReferenceTwoChapterwoVerses()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "4:3-5:6";

            Assert.Equal(4, passage.StartChapter);
            Assert.Equal(5, passage.EndChapter);
            Assert.Equal(3, passage.StartVerse);
            Assert.Equal(6, passage.EndVerse);
        }
        [Fact]
        public void ReferenceTwoChapterwoVersesSpaces()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "4:3 - 5:6";

            Assert.Equal(4, passage.StartChapter);
            Assert.Equal(5, passage.EndChapter);
            Assert.Equal(3, passage.StartVerse);
            Assert.Equal(6, passage.EndVerse);
        }
        [Fact]
        public void ReferenceJunk()
        {
            var context = _fixture.GetService<AppDbContext>();
            var passage = _faker.Passage;

            passage.Reference = "Junk";

            Assert.Equal(1, passage.StartChapter);
            Assert.Equal(1, passage.EndChapter);
            Assert.Equal(0, passage.StartVerse);
            Assert.Equal(0, passage.EndVerse);
        }
    }
}
