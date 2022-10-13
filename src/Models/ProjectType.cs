using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("projecttypes")]
    public partial class Projecttype : BaseModel
    {
        [Attr(PublicName = "name")]
        public string? Name { get; set; }

        [Attr(PublicName = "description")]
        public string? Description { get; set; }

    }
}
