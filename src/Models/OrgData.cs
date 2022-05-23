using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [NotMapped]
    public class Orgdata : BaseModel
    {
        public Orgdata()
        {
            Id = 1;
            Json = "{}";
        }
        [Attr(PublicName="json")]
        public string Json { get; set; }
        [Attr(PublicName="start-index")]
        public int StartIndex { get; set; }

    }
}
