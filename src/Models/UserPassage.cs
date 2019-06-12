using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class UserPassage : BaseModel, IArchive
    {
        [HasOne("user", Link.None)]
        public virtual User User { get; set; }

        public int UserId { get; set; }

        [HasOne("passage", Link.None)]
        public virtual Passage Passage { get; set; }
        
        public int PassageId { get; set; }

        [Attr("activity-name")]
        public string ActivityName { get; set; }

        [Attr("comment")]
        public string Comment { get; set; }

        public bool Archived { get; set; }
    }
}
