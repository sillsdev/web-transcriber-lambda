using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class SectionResourceUser : BaseModel, IArchive
    {

        [HasOne("section-resource", Link.None)]
        public virtual SectionResource SectionResource { get; set; }

        //[Attr("section-resource-id")]
        public int SectionResourceId { get; set; }

        [HasOne("user", Link.None)]
        public virtual User User { get; set; }

        //[Attr("user-id")]
        public int UserId { get; set; }

        public bool Archived { get; set; }
    }
}
