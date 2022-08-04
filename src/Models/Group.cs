using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    public partial class Group : BaseModel, IBelongsToOrganization, IArchive
    {
        public Group() : base()
        {
            Name = "";
        }
        [Attr(PublicName = "name")]
        public string Name { get; set; }

        [Attr(PublicName = "abbreviation")]
        public string? Abbreviation { get; set; }

        [Attr(PublicName = "all-users")]
        public bool AllUsers { get; set; }

        [HasOne(PublicName = "owner")]
        public virtual Organization Owner { get; set; }
        [Attr(PublicName = "owner-id")]
        public int OwnerId { get; set; }
        [Attr(PublicName = "permissions")]
        [Column(TypeName = "jsonb")]
        public string? Permissions { get; set; }

        /*
        [JsonIgnore]
        [HasMany(PublicName = "projects")]
        public virtual List<Project> Projects { get; set; }

        [JsonIgnore]
        [HasMany(PublicName = "group-memberships")]
        public virtual List<Groupmembership> GroupMemberships { get; set; }
        [NotMapped]
        public IEnumerable<int> UserIds => GroupMemberships.Select(g => g.UserId);
        [JsonIgnore]
        [NotMapped]
        public IEnumerable<User> Users => GroupMemberships.Select(g => g.User);
        */
        [NotMapped]
        public int OrganizationId { get => OwnerId; set { } }

        [NotMapped]
        public Organization Organization { get => Owner; set { } }

        public bool Archived { get; set; }
    }
}
