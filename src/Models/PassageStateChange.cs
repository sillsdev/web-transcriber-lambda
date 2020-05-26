using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class PassageStateChange : BaseModel
    {
        [Attr("passage-id")]
        public int PassageId { get; set; }
        [HasOne("passage", Link.None)]
        public virtual Passage Passage { get; set; }

        [Attr("state")]
        public string State { get; set; }
        [Attr("comments")]
        public string Comments { get; set; }

    }
}
