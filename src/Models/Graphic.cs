using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Graphics)]
    public partial class Graphic : BaseModel
    {
        [HasOne(PublicName = "organization")]
        public virtual Organization Organization { get; set; } = null!;

        [Attr(PublicName = "organization-id")]
        [ForeignKey(nameof(Organization))]
        public int OrganizationId { get; set; }

        [Attr(PublicName = "resource-type")]
        public string ResourceType { get; set; } = "";
        [Attr(PublicName = "resource-id")]
        public string ResourceId { get; set; } = "";
        [Attr(PublicName = "info")]
        [Column(TypeName = "jsonb")]
        public string? Info { get; set; } //json
    }
}
