using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class UserTask : BaseModel, ITrackDate
    {
        [HasOne("user", Link.None)]
        public virtual User User { get; set; }

        public int UserId { get; set; }

        [HasOne("task", Link.None)]
        public virtual Task Task { get; set; }
        
        public int TaskId { get; set; }

        [HasOne("project", Link.None)]
        public virtual Project Project { get; set; }
        public int ProjectId { get; set; }

        [Attr("activity-name")]
        public string ActivityName { get; set; }
        [Attr("state")]
        public string TaskState { get; set; }
        [Attr("comment")]
        public string Comment { get; set; }

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }
    }
}
