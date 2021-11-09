
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class OrgArtifactType: ArtifactTypeBase
    {
        public int OrganizationId { get; set; }
        [Attr("organization")]
        public Organization Organization { get; set; }
    }
}
