using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [Table("invitations")]
    public partial class Invitation : BaseModel, IBelongsToOrganization
    {
        [Attr(PublicName = "email")]
        public string? Email { get; set; }

        [Attr(PublicName = "accepted")]
        public bool Accepted { get; set; }

        [Attr(PublicName = "login-link")]
        public string? LoginLink { get; set; }

        [Attr(PublicName = "invited-by")]
        public string? InvitedBy { get; set; }

        [NotMapped]
        [Attr(PublicName = "strings")]
        public string Strings { get; set; } = "";

        [HasOne(PublicName = "organization")]
        public virtual Organization Organization { get; set; } = null!;
        public int OrganizationId { get; set; }

        [HasOne(PublicName = "role")]
        public virtual Role? Role { get; set; }
        public int RoleId { get; set; }

        [HasOne(PublicName = "group")]
        public virtual Group? Group { get; set; }
        public int? GroupId { get; set; }

        [HasOne(PublicName = "group-role")]
        public virtual Role? GroupRole { get; set; }
        public int? GroupRoleId { get; set; }

        [HasOne(PublicName = "all-users-role")]
        public virtual Role? AllUsersRole { get; set; }
        public int AllUsersRoleId { get; set; }
    }
}
