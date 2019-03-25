using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class OrganizationMembership :BaseModel
    {
        [NotMapped]
        [Attr("email")]
        public string Email { get; set; }

        [HasOne("user")]
        public virtual User User { get; set; }

        [Attr("user-id")]
        public int UserId { get; set; }

        [HasOne("organization")]
        public virtual Organization Organization { get; set; }

        [Attr("organization-id")]
        public int OrganizationId { get; set; }
    }
}
