using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class TaskSet : BaseModel
    {
        [Attr("task-id")]
        public int TaskId { get; set; }
        [HasOne("task")]
        public virtual Task Task { get; set; }

        [Attr("set-id")]
        public int SetId { get; set; }

        [HasOne("set")]
        public virtual Set Set { get; set; }
    }
}
