
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class OrgArtifactCategory : ArtifactCategoryBase
    {
        public int OrganizationId { get; set; }
        [Attr("organization")]
        public Organization Organization { get; set; }
    }
}
