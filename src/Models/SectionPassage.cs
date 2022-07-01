using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public class Sectionpassage : BaseModel
    {
        [Attr(PublicName = "data")]
        public string? Data { get; set; }
        [Attr(PublicName = "plan-id")]
        public int PlanId { get; set; }
        [Attr(PublicName = "uuid")]
        public Guid Uuid { get; set; }

        public bool Complete { get; set; }
    }
}
