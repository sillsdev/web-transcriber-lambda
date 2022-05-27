using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table("organizations")]
    public partial class Organization : BaseModel, IArchive
    {
        public Organization(): base()
        {
            Slug = "";
            Groups = new List<Group>();
            Projects = new List<Project>();
        }
        [Attr(PublicName="name")]
        public string? Name { get; set; }

        [Attr(PublicName="website-url")]
        public string? WebsiteUrl { get; set; }

        [Attr(PublicName="description")]
        public string? Description { get; set; }

        [Attr(PublicName="logo-url")]
        public string? LogoUrl { get; set; }

        /* local fields */
        [Attr(PublicName="slug")]
        public string Slug { get; set; }
        
        [Attr(PublicName="public-by-default")]
        public bool? PublicByDefault { get; set; }

        [Attr(PublicName="default-params")]
        [Column(TypeName = "jsonb")]
        public string? DefaultParams { get; set; } 

        [NotMapped]
        [Attr(PublicName="all-users-name")]
        public string? AllUsersName { get; set; }

        [HasOne(PublicName="owner")]
        public virtual User? Owner { get; set; }
        public int? OwnerId { get; set; }

        [HasMany(PublicName="organization-memberships")]
        [JsonIgnore]
        public List<Organizationmembership>? OrganizationMemberships { get; set; }

        [JsonIgnore]
        [HasMany(PublicName="groups")]
        public List<Group> Groups { get; set; }

        [JsonIgnore]
        [HasMany(PublicName="projects")]
        public List<Project> Projects { get; set; }

        public bool Archived { get; set; }
    }
}
