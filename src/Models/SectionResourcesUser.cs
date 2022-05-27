using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public partial class Sectionresourceuser : BaseModel, IArchive
    {

        [HasOne(PublicName = "sectionresource")]
        public virtual Sectionresource? SectionResource { get; set; }

        //[Attr("section-resource-id")]
        public int SectionResourceId { get; set; }

        [HasOne(PublicName = "user")]
        public virtual User? User { get; set; }

        //[Attr("user-id")]
        public int UserId { get; set; }

        public bool Archived { get; set; }
    }
}
