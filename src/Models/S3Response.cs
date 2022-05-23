using System.IO;

namespace SIL.Transcriber.Models
{
    public class S3Response : FileResponse
    {
        public Stream? FileStream { get; set; }
    }
}
