using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public class Set : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("project-id")]
        public int ProjectId { get; set; }
        [HasOne("project")]
        public virtual Project Project { get; set; }

        [Attr("book-id")]
        public int? BookId { get; set; }

        [HasOne("book")]
        public virtual Book Book { get; set; }

        [NotMapped]
        [HasManyThrough(nameof(TaskSets))]
        public List<Task> Tasks { get; set; }
        public List<TaskSet> TaskSets { get; set; }

    }
}
