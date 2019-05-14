using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class UserRole : BaseModel
    {
        [Attr("user-id")]
        public int UserId { get; set; }

        [HasOne("user", Link.None)]
        public virtual User User { get; set; }


        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }

        [Attr("role-id")]
        public int RoleId { get; set; }

        [HasOne("organization", Link.None)]
        public virtual Organization Organization { get; set; }
        [Attr("organization-id")]
        public int OrganizationId { get; set; }

        [NotMapped]
        public RoleName RoleName
        {
            get { return Role.Rolename; }
        }


    }
}
