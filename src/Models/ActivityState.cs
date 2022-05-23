
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public partial class Activitystate : BaseModel
    {
        [Attr(PublicName="state")]
        public string? State { get; set; }
        [Attr(PublicName="sequencenum")]
        public int Sequencenum { get; set; }
    }
}
