using System;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class DataChanges : BaseModel
    {
        [NotMapped]
        [Attr("startnext")]
        public int Startnext { get; set; }
        [NotMapped]
        [Attr("querydate")] 
        public DateTime Querydate { get; set; }
        [NotMapped]
        [Attr("changes")]  
        public OrbitId[] Changes { get; set; }
        [NotMapped]
        [Attr("deleted")]  
        public OrbitId[] Deleted { get; set; }
    }
}

