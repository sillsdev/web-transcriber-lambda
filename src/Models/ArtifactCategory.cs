using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class ArtifactCategoryBase : BaseModel, IArchive
    {
        [Attr("categoryname")]
        public string Categoryname { get; set; }
        public bool Archived { get; set; }
    }
    public class ArtifactCategory : ArtifactCategoryBase { }
}