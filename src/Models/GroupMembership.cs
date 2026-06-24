using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.GroupMemberships)]
    public partial class Groupmembership : BaseModel, IArchive
    {
        [HasOne(PublicName = "user")]
        public virtual User User { get; set; } = null!;

        [Attr(PublicName = "user-id")]
        public int UserId { get; set; }

        [HasOne(PublicName = "group")]
        public virtual Group Group { get; set; } = null!;

        [Attr(PublicName = "group-id")]
        public int GroupId { get; set; }

        [HasOne(PublicName = "role")]
        public virtual Role Role { get; set; } = null!;

        [Attr(PublicName = "role-id")]
        public int RoleId { get; set; }

        [NotMapped]
        public RoleName RoleName {
            get { return Role == null ? RoleName.Transcriber : Role.Rolename; }
        }
        [Attr(PublicName = "font")]
        public string? Font { get; set; }

        [Attr(PublicName = "font-size")]
        public string? FontSize { get; set; }

        public bool Archived { get; set; }
    }
}
