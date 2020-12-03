using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    
    public class ProjData : BaseModel
    {

           public ProjData()
            {
                Id = 1;
            }
            [NotMapped]
            [Attr("json")]
            public string Json { get; set; }
            [NotMapped]
            [Attr("startnext")]
            public int Startnext { get; set; }
            [NotMapped]
            [Attr("projectid")]
            public int ProjectId { get; set; }
            [NotMapped]
            [Attr("snapshotdate")]
            public string SnapshotDate { get; set; }
    }
}
