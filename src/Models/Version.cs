using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class Version : BaseModel
    {
        [Attr("desktop-version")]
        public string DesktopVersion { get; set; }

        public int SchemaVersion { get; set; }
    }
}
