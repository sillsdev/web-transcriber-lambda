
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
   public class Artifacttype : BaseModel, IArchive
    {
       
        [Attr(PublicName = "typename")]
        public string? Typename { get; set; }
        public bool Archived { get; set; }
        public int? OrganizationId { get; set; }
        [HasOne(PublicName = "organization")]
        public Organization? Organization { get; set; }
    }
}
