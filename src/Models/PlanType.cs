using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [Table("plantypes")]
    public partial class PlanType : BaseModel
    {
        public PlanType():base()
        {
            Name = "";
        }
        [Attr(PublicName="name")]
        public string Name { get; set; }

        [Attr(PublicName="description")]
        public string? Description { get; set; }

    }
}
