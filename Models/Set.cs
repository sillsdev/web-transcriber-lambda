using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class Set : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("project-id")]
        public int ProjectId { get; set; }
        [HasMany("project")]
        public virtual Project Project { get; set; }

        [Attr("book-id")]
        public int? BookId { get; set; }

        [HasOne("book")]
        public virtual Book Book { get; set; }

        [HasMany("task-sets")]
        public virtual List<TaskSet> Tasksets { get; set; }
    }
}
