using JsonApiDotNetCore.Resources.Annotations;
namespace SIL.Transcriber.Models
{
    public partial class PassageSection : BaseModel, IArchive
    {
        [Attr(PublicName="passage-id")]
        public int PassageId { get; set; }
        [HasOne(PublicName = "passage")]
        public virtual Passage? Passage { get; set; }

        [Attr(PublicName = "section-id")]
        public int SectionId { get; set; }

        [HasOne(PublicName = "section")]
        public virtual Section? Section { get; set; }

        public bool Archived { get; set; }
    }
}
