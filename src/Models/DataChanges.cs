using System;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class DataChanges : Identifiable<int> //GRRR forced to put this here by JSONAPI
    {
        [Attr("querydate")] 
        public DateTime Querydate { get; set; }
        [Attr("changes")]  
        public OrbitId[][] Changes { get; set; }
        [Attr("deleted")]  
        public OrbitId[][] Deleted { get; set; }
    }
}

