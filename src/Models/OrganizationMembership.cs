using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [Table("organizationmemberships")]
    public partial class OrganizationMembership :BaseModel, IArchive
    {
        public OrganizationMembership():base()
        {
            User = new User();
            Organization = new Organization();
            Role = new Role();
        }
        [NotMapped]
        [Attr(PublicName="email")]  //user's email?
        public string? Email { get; set; }

        [HasOne(PublicName="user")]
        public virtual User User { get; set; }

        [Attr(PublicName="user-id")]
        public int UserId { get; set; }

        [HasOne(PublicName="organization")]
        public virtual Organization Organization { get; set; }

        [Attr(PublicName="organization-id")]
        public int OrganizationId { get; set; }

        [HasOne(PublicName="role")]
        public Role Role { get; set; }
        public int RoleId { get; set; }

        [NotMapped]
        public RoleName RoleName
        {
            get { return (RoleName)RoleId; }
        }
        public bool Archived { get; set; }
    }
}
