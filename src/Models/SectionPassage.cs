using JsonApiDotNetCore.Models;
using System;

namespace SIL.Transcriber.Models
{
    public class SectionPassage : BaseModel
    {
        [Attr("data")]
        public string Data { get; set; }
        [Attr("plan-id")]
        public int PlanId { get; set; }
        [Attr("uuid")]
        public Guid uuid { get; set; }

        public bool Complete { get; set; }
    }
}
