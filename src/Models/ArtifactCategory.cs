
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class ArtifactCategory : BaseModel, IArchive
    {
        [Attr("categoryname")]
        public string Categoryname { get; set; }
        public bool Archived { get; set; }
        public int? OrganizationId { get; set; }
        [HasOne("organization", Link.None)]
        public Organization Organization { get; set; }
    }
}
