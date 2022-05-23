using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [Table("groupmemberships")]
    public partial class GroupMembership : BaseModel, IArchive
    {
        public GroupMembership():base()
        {
            User = new User();
            Group = new Group();
            Role = new Role();
        }
        [HasOne(PublicName="user")]
        public virtual User User { get; set; }
        public int UserId { get; set; }

        [HasOne(PublicName="group")]
        public virtual Group Group { get; set; }
        public int GroupId { get; set; }

        [HasOne(PublicName="role")]
        public virtual Role Role { get; set; }
        public int RoleId { get; set; }

        [NotMapped]
        public RoleName RoleName
        {
            get { return Role == null ? RoleName.Transcriber : Role.Rolename; }
        }
        [Attr(PublicName="font")]
        public string? Font { get; set; }

        [Attr(PublicName="font-size")]
        public string? FontSize { get; set; }

        public bool Archived { get; set; }
    }
}
