using JsonApiDotNetCore.Models;
using System.IO;
using System.Net;

namespace SIL.Transcriber.Models
{ /* This is an Identifiable only so that we can pass it back without JsonApiDotNetCore puking on it */
    public class FileResponse : Identifiable<int>
    {
        [Attr("status")]
        public HttpStatusCode Status { get; set; }
        [Attr("message")]
        public string Message { get; set; }
        [Attr("fileurl")]
        public string FileURL { get; set; }
        [Attr("contenttype")]
        public string ContentType { get; set; }
    }
}
