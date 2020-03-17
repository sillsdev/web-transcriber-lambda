using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class SectionPassage : Identifiable<int>
    {
        [NotMapped]
        [Attr("data")]
        public string Data { get; set; }
    }
}
