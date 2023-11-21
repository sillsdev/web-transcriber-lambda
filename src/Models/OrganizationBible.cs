using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.OrganizationBibles)]
    public partial class Organizationbible : BaseModel, IArchive
    {
        [HasOne(PublicName = "bible")]
        public virtual Bible Bible { get; set; } = null!;

        [Attr(PublicName = "bible-id")]
        public int BibleId { get; set; }

        [HasOne(PublicName = "organization")]
        public virtual Organization Organization { get; set; } = null!;

        [Attr(PublicName = "organization-id")]
        public int OrganizationId { get; set; }

        [Attr(PublicName = "ownerorg")]
        public bool Ownerorg { get; set; }
        public bool Archived { get; set; }
    }
}
