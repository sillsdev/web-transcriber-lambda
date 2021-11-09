
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class ArtifactTypeBase : BaseModel, IArchive
    {
        [Attr("typename")]
        public string Typename { get; set; }
        public bool Archived { get; set; }
    }
    public class ArtifactType : ArtifactTypeBase { }
}
