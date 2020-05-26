using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class Group : BaseModel, IBelongsToOrganization, IArchive
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("abbreviation")]
        public string Abbreviation { get; set; }

        [Attr("all-users")]
        public bool AllUsers { get; set; }

        [HasOne("owner", Link.None)]
        public virtual Organization Owner { get; set; }
        [Attr("owner-id")]
        public int OwnerId { get; set; }
        
        [HasMany("projects", Link.None)]
        public virtual List<Project> Projects { get; set; }

        [HasMany("group-memberships", Link.None)]
        public virtual List<GroupMembership> GroupMemberships { get; set; }

        [NotMapped]
        public int OrganizationId { get => OwnerId; set { } }
        
        [NotMapped]
        public Organization Organization { get => Owner; set { } }

        [NotMapped]
        public IEnumerable<int> UserIds => GroupMemberships?.Select(g => g.UserId);

        [NotMapped]
        public IEnumerable<User> Users => GroupMemberships?.Select(g => g.User);

        public bool Archived { get; set; }
    }
}
