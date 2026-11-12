using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public partial class Mediareplacement : BaseModel, IArchive
    {
        [Attr(PublicName = "plan-id")]
        public int PlanId { get; set; }

        [HasOne(PublicName = "passage")]
        required public Passage Passage { get; set; }

        [Attr(PublicName = "replacement-mediafile-id")]
        public int ReplacementMediafileId { get; set; }

        [HasOne(PublicName = "replacement-mediafile")]
        required public Mediafile ReplacementMediafile { get; set; }

        [Attr(PublicName = "selection-start")]
        public double? SelectionStart { get; set; }

        [Attr(PublicName = "selection-end")]
        public double? SelectionEnd { get; set; }

        [Attr(PublicName = "output-mediafile-id")]
        public int OutputMediafileId { get; set; }

        [HasOne(PublicName = "output-mediafile")]
        required public Mediafile OutputMediafile { get; set; }

        public bool Archived { get; set; }
    }
}
