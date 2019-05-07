using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class PassageSection : BaseModel
    {
        [Attr("passage-id")]
        public int PassageId { get; set; }
        [HasOne("passage")]
        public virtual Passage Passage { get; set; }

        [Attr("section-id")]
        public int SectionId { get; set; }

        [HasOne("Section")]
        public virtual Section Section { get; set; }
    }
}
