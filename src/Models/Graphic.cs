using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Graphics)]
    public partial class Graphic : BaseModel, IBelongsToOrganization, IArchive
    {
        [HasOne(PublicName = "organization")]
        public virtual Organization Organization { get; set; } = null!;

        [Attr(PublicName = "organization-id")]
        [ForeignKey(nameof(Organization))]
        public int OrganizationId { get; set; }
        [HasOne(PublicName = "mediafile")]
        public virtual Mediafile Mediafile { get; set; } = null!;

        [Attr(PublicName = "mediafile-id")]
        [ForeignKey(nameof(Mediafile))]
        public int MediafileId { get; set; }
        [Attr(PublicName = "resource-type")]
        public string ResourceType { get; set; } = "";
        [Attr(PublicName = "resource-id")]
        public string ResourceId { get; set; } = "";
        [Attr(PublicName = "info")]
        [Column(TypeName = "jsonb")]
        public string? Info { get; set; } //json
        public bool Archived { get; set; }
    }
}
