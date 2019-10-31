using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class OrganizationMembership :BaseModel, IArchive
    {
        [NotMapped]
        [Attr("email")]  //user's email?
        public string Email { get; set; }

        [HasOne("user")]
        public virtual User User { get; set; }

        [Attr("user-id")]
        public int UserId { get; set; }

        [HasOne("organization")]
        public virtual Organization Organization { get; set; }

        [Attr("organization-id")]
        public int OrganizationId { get; set; }

        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }
        public int RoleId { get; set; }

        [NotMapped]
        public RoleName RoleName
        {
            get { return Role == null ? RoleName.Member : Role.Rolename; }
        }
        public bool Archived { get; set; }
    }
}
