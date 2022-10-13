using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public partial class Datachanges : BaseModel
    {
        public Datachanges() : base()
        {
            Changes = new OrbitId [0];
            Deleted = new OrbitId [0];
        }
        [NotMapped]
        [Attr(PublicName = "startnext")]
        public int Startnext { get; set; }
        [NotMapped]
        [Attr(PublicName = "querydate")]
        public DateTime Querydate { get; set; }
        [NotMapped]
        [Attr(PublicName = "changes")]
        public OrbitId [] Changes { get; set; }
        [NotMapped]
        [Attr(PublicName = "deleted")]
        public OrbitId [] Deleted { get; set; }
    }
}

