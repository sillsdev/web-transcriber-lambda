using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("passagestatechanges")]
    public partial class Passagestatechange : BaseModel
    {

        [Attr(PublicName = "passage-id")]
        public int PassageId { get; set; }
        [HasOne(PublicName = "passage")]
        public Passage Passage { get; set; } = null!;

        [Attr(PublicName = "state")]
        public string? State { get; set; }
        [Attr(PublicName = "comments")]
        public string? Comments { get; set; }

    }
}
