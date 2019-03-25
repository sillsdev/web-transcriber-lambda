using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Organization : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("website-url")]
        public string WebsiteUrl { get; set; }

        [Attr("logo-url")]
        public string LogoUrl { get; set; }

        [Attr("public-by-default")]
        public bool? PublicByDefault { get; set; } = true;

        [HasOne("owner")]
        public virtual User Owner { get; set; }
        public int OwnerId { get; set; }

        [HasMany("organization-memberships", Link.None)]
        public virtual List<OrganizationMembership> OrganizationMemberships { get; set; }
    }
}
