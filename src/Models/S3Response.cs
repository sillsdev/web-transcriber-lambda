using System.IO;

namespace SIL.Transcriber.Models
{
    public class S3Response : Fileresponse
    {
        public Stream? FileStream { get; set; }
    }
}
