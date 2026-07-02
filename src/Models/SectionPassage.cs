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

        [Attr(PublicName = "processing")]
        public bool Processing { get; set; }

        [Attr(PublicName = "processing-started")]
        public DateTime? ProcessingStarted { get; set; }

        [Attr(PublicName = "complete")]
        public bool Complete { get; set; }
    }
}
