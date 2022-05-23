using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
     [Table("passagestatechanges")]
     public partial class PassageStateChange : BaseModel
    {
        public PassageStateChange():base()
        {
            Passage = new();
        }
        [Attr(PublicName="passage-id")]
        public int PassageId { get; set; }
        [HasOne(PublicName="passage")]
        public virtual Passage Passage { get; set; }

        [Attr(PublicName="state")]
        public string? State { get; set; }
        [Attr(PublicName="comments")]
        public string? Comments { get; set; }

    }
}
