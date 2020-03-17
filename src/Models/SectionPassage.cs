using JsonApiDotNetCore.Models;
using System;

namespace SIL.Transcriber.Models
{
    public class SectionPassage : Identifiable<int>
    {
        [Attr("data")]
        public string Data { get; set; }
        [Attr("uuid")]
        public Guid uuid { get; set; }

        public bool Complete { get; set; }
    }
}
