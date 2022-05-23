using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public partial class SectionResourceUser : BaseModel, IArchive
    {

        [HasOne(PublicName = "sectionresource")]
        public virtual SectionResource? SectionResource { get; set; }

        //[Attr("section-resource-id")]
        public int SectionResourceId { get; set; }

        [HasOne(PublicName = "user")]
        public virtual User? User { get; set; }

        //[Attr("user-id")]
        public int UserId { get; set; }

        public bool Archived { get; set; }
    }
}
