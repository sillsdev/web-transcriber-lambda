using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using System.Net;

namespace SIL.Transcriber.Models
{ /* This is an Identifiable only so that we can pass it back without JsonApiDotNetCore puking on it */
    public class Fileresponse : Identifiable<int>
    {
        public Fileresponse() : base()
        {
            Message = "";
            FileURL = "";
            ContentType = "application/json";
        }

        [Attr(PublicName = "status")]
        public HttpStatusCode Status { get; set; }

        [Attr(PublicName = "message")]
        public string Message { get; set; }

        [Attr(PublicName = "fileurl")]
        public string FileURL { get; set; }

        [Attr(PublicName = "contenttype")]
        public string ContentType { get; set; }
    }
#pragma warning disable IDE1006 // Naming Styles
    public class JFRData
    {
        public JFRData() : base()
        {
            attributes = new();
            type = "";
        }

        public JFRAttributes attributes { get; set; }
        public string type { get; set; }
        public int id { get; set; }
    }

    public class JFRAttributes
    {
        public JFRAttributes() : base()
        {
            message = "";
            fileurl = "";
            contenttype = "application/json";
        }

        public string message { get; set; }
        public string fileurl { get; set; }
        public string contenttype { get; set; }
    }

    public class JsonedFileresponse
    {
        public JsonedFileresponse() : base()
        {
            data = new();
        }

        public JFRData data;
    }
#pragma warning restore IDE1006 // Naming Styles
}
