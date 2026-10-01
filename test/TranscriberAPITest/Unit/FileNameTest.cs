using SIL.Transcriber.Utility;
using Xunit;

namespace TranscriberAPI.Tests.Unit;

public class FileNameTest
{
    [Fact]
    public void S3ObjectName_strips_signed_url_and_huge_query()
    {
        string url = "https://bucket.s3.amazonaws.com/org/plan/clip.webm?X-Amz-Algorithm=AWS4-HMAC-SHA256&X-Amz-Security-Token=" + new string('A', 9000);
        Assert.Equal("clip.webm", FileName.S3ObjectName(url));
    }

    [Fact]
    public void S3ObjectName_keeps_plain_filename()
    {
        Assert.Equal("take1.mp3", FileName.S3ObjectName("take1.mp3"));
    }
}
