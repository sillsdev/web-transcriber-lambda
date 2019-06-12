using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class UserPassage : BaseModel
    {
        [HasOne("user", Link.None)]
        public virtual User User { get; set; }

        [Attr("user-id")]
        public int UserId { get; set; }

        [HasOne("passage", Link.None)]
        public virtual Passage Passage { get; set; }
        
        public int PassageId { get; set; }

        [Attr("activity-name")]
        public string ActivityName { get; set; }

        [Attr("comment")]
        public string Comment { get; set; }

        [Attr("role-id")]
        public int  RoleId { get; set; }
        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }
    }
}
