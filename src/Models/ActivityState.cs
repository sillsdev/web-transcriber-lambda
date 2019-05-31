using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class ActivityState : BaseModel
    {
        [Attr("state")]
        public string State { get; set; }
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }
    }
}
