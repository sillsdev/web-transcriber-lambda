using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class UserPassage : BaseModel, ITrackDate
    {
        [HasOne("user", Link.None)]
        public virtual User User { get; set; }

        public int UserId { get; set; }

        [HasOne("passage", Link.None)]
        public virtual Passage Passage { get; set; }
        
        public int PassageId { get; set; }

        [Attr("activity-name")]
        public string ActivityName { get; set; }
        [Attr("state")]
        public string State { get; set; }
        [Attr("comment")]
        public string Comment { get; set; }

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }
    }
}
