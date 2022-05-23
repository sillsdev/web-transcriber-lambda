using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public class Version : BaseModel
    {
        [Attr(PublicName="desktopVersion")]
        public string? DesktopVersion { get; set; }

        public int? SchemaVersion { get; set; }
    }
}
