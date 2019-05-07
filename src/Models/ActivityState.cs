using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class ActivityState : BaseModel
    {
        [Attr("state")]
        public string State { get; set; }
    }
}
