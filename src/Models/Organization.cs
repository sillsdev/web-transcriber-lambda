using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Organization : BaseModel, IArchive
    {
        /* from identity */
        [Attr("sil-id")]
        public int SilId { get; set; }

        [Attr("name")]
        public string Name { get; set; }

        [Attr("website-url")]
        public string WebsiteUrl { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [Attr("logo-url")]
        public string LogoUrl { get; set; }

        /* local fields */
        [Attr("slug")]
        public string Slug { get; set; }
        
        [Attr("public-by-default")]
        public bool? PublicByDefault { get; set; } = true;

        [Attr("default-params")]
        [Column(TypeName = "jsonb")]
        public string DefaultParams { get; set; } 

        [NotMapped]
        [Attr("all-users-name")]
        public string AllUsersName { get; set; }

        [HasOne("owner", Link.None)]
        public virtual User Owner { get; set; }
        public int? OwnerId { get; set; }

        [HasMany("organization-memberships", Link.None)]
        public List<OrganizationMembership> OrganizationMemberships { get; set; }

        [HasMany("groups", Link.None)]
        public List<Group> Groups { get; set; }

        [HasMany("projects", Link.None)]
        public List<Project> Projects { get; set; }

        public bool Archived { get; set; }
    }
}
