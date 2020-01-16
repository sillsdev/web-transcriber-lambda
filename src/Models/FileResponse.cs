using System.IO;
using System.Net;

namespace SIL.Transcriber.Models
{
    public class FileResponse
    {
        public HttpStatusCode Status { get; set; }
        public string Message { get; set; }
        public Stream FileStream { get; set; }
        public string ContentType { get; set; }
    }
}
