using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("plantypes")]
    public partial class Plantype : BaseModel
    {
        public Plantype() : base()
        {
            Name = "";
        }
        [Attr(PublicName = "name")]
        public string Name { get; set; }

        [Attr(PublicName = "description")]
        public string? Description { get; set; }

    }
}
