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

        [Attr("passage-id")]
        public int PassageId { get; set; }


        [Attr("role-id")]
        public int  RoleId { get; set; }
        [HasOne("role", Link.None)]
        public virtual Role Role { get; set; }

        [Attr("comment")]
        public string Comment { get; set; }
    }
}
