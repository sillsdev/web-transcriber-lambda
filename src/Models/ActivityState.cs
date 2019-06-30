using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Activitystate : BaseModel
    {
        [Attr("state")]
        public string State { get; set; }
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }
    }
}
