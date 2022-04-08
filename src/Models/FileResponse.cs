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

        public JsonedFileResponse Twiddle() {
            return new JsonedFileResponse
            {
                data = new JFRData
                {
                    attributes = new JFRAttributes
                    {
                        message = Message,
                        fileurl = FileURL,
                        contenttype = ContentType,
                    },
                    id = Id,
                }
            };
        }
    }
#pragma warning disable IDE1006 // Naming Styles
    public class JFRData
    {
        public JFRAttributes attributes { get; set; }
        public string type { get; set; }
        public int id { get; set; }
    }
    public class JFRAttributes
    {
        public string message {get; set;}
        public string fileurl { get; set; }
        public string contenttype { get; set; }
    }
    public class JsonedFileResponse
    {
        public JFRData data;
    }
#pragma warning restore IDE1006 // Naming Styles
}
