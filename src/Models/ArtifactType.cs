
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class ArtifactType : BaseModel, IArchive
    {
       
        [Attr("typename")]
        public string Typename { get; set; }
        public bool Archived { get; set; }
        public int? OrganizationId { get; set; }
        [HasOne("organization", Link.None)]
        public Organization Organization { get; set; }
    }
}
