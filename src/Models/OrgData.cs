using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [NotMapped]
    public class Orgdata : BaseModel
    {
        public Orgdata()
        {
            Id = 1;
            Json = "{}";
            LastModifiedOrigin = "apix";
        }
        [Attr(PublicName = "json")]
        public string Json { get; set; }
        [Attr(PublicName = "start-index")]
        public int StartIndex { get; set; }
    }
}
