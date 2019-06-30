using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Organization : BaseModel, IArchive
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("slug")]
        public string Slug { get; set; }

        [Attr("website-url")]
        public string WebsiteUrl { get; set; }

        [Attr("logo-url")]
        public string LogoUrl { get; set; }

        [Attr("public-by-default")]
        public bool? PublicByDefault { get; set; } = true;

        [HasOne("owner")]
        public virtual User Owner { get; set; }
        public int OwnerId { get; set; }
        
        [NotMapped]
        [HasManyThrough(nameof(OrganizationMemberships))]
        public List<User> Users { get; set; }
        public List<OrganizationMembership> OrganizationMemberships { get; set; }

        [HasMany("groups")]
        public virtual List<Group> Groups { get; set; }

        [HasMany("user-roles", Link.None)]
        public virtual List<UserRole> UserRoles { get; set; }


        /*
        [HasMany("organization-memberships", Link.None)]
        public virtual List<OrganizationMembership> OrganizationMemberships { get; set; }

        [NotMapped]
        [HasMany("userids")]
        public IEnumerable<int> UserIds => OrganizationMemberships?.Select(om => om.UserId);
        [NotMapped]
        [HasMany("users")]
        public IEnumerable<User> Users => OrganizationMemberships?.Select(om => om.User);
        */
        public bool Archived { get; set; }
    }
}
