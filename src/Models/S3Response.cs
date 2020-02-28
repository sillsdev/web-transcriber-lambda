using System.IO;
using System.Net;

namespace SIL.Transcriber.Models
{
    public class S3Response : FileResponse
    {
        public Stream FileStream { get; set; }
    }
}
