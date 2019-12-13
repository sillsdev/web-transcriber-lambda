using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class GroupMembership : BaseModel, IArchive
    {
        [HasOne("user")]
        public virtual User User { get; set; }
        public int UserId { get; set; }

        [HasOne("group")]
        public virtual Group Group { get; set; }
        public int GroupId { get; set; }

        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }
        public int RoleId { get; set; }

        [NotMapped]
        public RoleName RoleName
        {
            get { return Role == null ? RoleName.Transcriber : Role.Rolename; }
        }
        [Attr("font")]
        public string Font { get; set; }

        [Attr("font-size")]
        public string FontSize { get; set; }

        public bool Archived { get; set; }
    }
}
