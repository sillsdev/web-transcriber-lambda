using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Organizations)]
    public partial class Organization : BaseModel, IArchive
    {
        public Organization() : base()
        {
            Slug = "";
        }
        [Attr(PublicName = "name")]
        public string? Name { get; set; }

        [Attr(PublicName = "website-url")]
        public string? WebsiteUrl { get; set; }

        [Attr(PublicName = "description")]
        public string? Description { get; set; }

        [Attr(PublicName = "logo-url")]
        public string? LogoUrl { get; set; }

        /* local fields */
        [Attr(PublicName = "slug")]
        public string Slug { get; set; }

        [Attr(PublicName = "public-by-default")]
        public bool? PublicByDefault { get; set; }
        [Attr(PublicName = "clusterbase")]
        public bool ClusterBase { get; set; }

        [Attr(PublicName = "cluster-id")]
        public int? ClusterId { get; set; }
        [HasOne(PublicName = "cluster")]
        public virtual Organization? Cluster { get; set; }

        [Attr(PublicName = "default-params")]
        [Column(TypeName = "jsonb")]
        public string? DefaultParams { get; set; }

        [NotMapped]
        [Attr(PublicName = "all-users-name")]
        public string? AllUsersName { get; set; }

        [HasOne(PublicName = "owner")]
        public virtual User? Owner { get; set; }
        public int? OwnerId { get; set; }

        public bool Archived { get; set; }
    }
}
