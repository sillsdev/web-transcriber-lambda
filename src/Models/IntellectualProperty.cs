using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table(Tables.IntellectualPropertys)]
    public partial class Intellectualproperty : BaseModel, IArchive
    {
        public Intellectualproperty() : base()
        {
        }
        public int OrganizationId { get; set; }
        [HasOne(PublicName = "organization")]
        public Organization? Organization { get; set; }

        [Attr(PublicName = "rights-holder")]
        public string RightsHolder { get; set; } = string.Empty;

        [Attr(PublicName = "notes")]
        public string? Notes { get; set; }
        public int? ReleaseMediafileId { get; set; }
        [HasOne(PublicName = "release-mediafile")]
        public Mediafile? ReleaseMediafile { get; set; }

        [Attr(PublicName = "offline-id")]
        public string? OfflineId { get; set; }
        [Attr(PublicName = "offline-mediafile-id")]
        public string? OfflineMediafileId { get; set; }

        public bool Archived { get; set; }
    }
}
