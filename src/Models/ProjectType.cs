using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.ProjectTypes)]
    public partial class Projecttype : BaseModel
    {
        [Attr(PublicName = "name")]
        public string? Name { get; set; }

        [Attr(PublicName = "description")]
        public string? Description { get; set; }

    }
}
