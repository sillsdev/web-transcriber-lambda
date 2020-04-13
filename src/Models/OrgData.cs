using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class OrgData : BaseModel
    {
        public OrgData()
        {
            Id = 1;
        }
        [NotMapped]
        [Attr("json")]
        public string Json { get; set; }
        [NotMapped]
        [Attr("startnext")]
        public int Startnext { get; set; }
    }
}
