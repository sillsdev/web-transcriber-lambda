using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [Table("projecttypes")]
    public partial class ProjectType : BaseModel
    {
        [Attr(PublicName="name")]
        public string? Name { get; set; }

        [Attr(PublicName="description")]
        public string? Description { get; set; }

   }
}
