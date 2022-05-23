using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    public partial class Group : BaseModel, IBelongsToOrganization, IArchive
    {
        public Group() :base ()
        {
            Name = "";
            GroupMemberships = new List<GroupMembership>();
            Projects = new List<Project> ();
            Owner = new Organization();
        }
        [Attr(PublicName="name")]
        public string Name { get; set; }

        [Attr(PublicName="abbreviation")]
        public string? Abbreviation { get; set; }

        [Attr(PublicName="all-users")]
        public bool AllUsers { get; set; }

        [HasOne(PublicName="owner")]
        public virtual Organization Owner { get; set; }
        [Attr(PublicName="owner-id")]
        public int OwnerId { get; set; }

        [JsonIgnore]
        [HasMany(PublicName="projects")]
        public virtual List<Project> Projects { get; set; }

        [JsonIgnore]
        [HasMany(PublicName="group-memberships")]
        public virtual List<GroupMembership> GroupMemberships { get; set; }

        [NotMapped]
        public int OrganizationId { get => OwnerId; set { } }
        
        [NotMapped]
        public Organization Organization { get => Owner; set { } }

        [NotMapped]
        public IEnumerable<int> UserIds => GroupMemberships.Select(g => g.UserId);

        [JsonIgnore]
        [NotMapped]
        public IEnumerable<User> Users => GroupMemberships.Select(g => g.User);

        public bool Archived { get; set; }
    }
}
