using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.PassageTypes)]
    public partial class Passagetype : BaseModel
    {
        [Attr(PublicName = "usfm")]
        public string USFM { get; set; } = "";

        [Attr(PublicName = "title")]
        public string? Title { get; set; }
        [Attr(PublicName = "abbrev")]
        public string Abbrev { get; set; } = "";

        [Attr(PublicName = "default-order")]
        public int DefaultOrder { get; set; }

    }
}
