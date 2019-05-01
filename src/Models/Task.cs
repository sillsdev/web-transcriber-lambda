using System;
using System.Collections.Generic;
using System.Collections;
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Task : BaseModel, ITrackDate
    {
        [Attr("reference")]
        public string Reference { get; set; }
        [Attr("passage")]
        public string Passage { get; set; }
        [Attr("position")]
        public double? Position { get; set; }
        [Attr("taskstate")]
        public string TaskState { get; set; }
        [Attr("hold")]
        public Boolean Hold { get; set; }
        [Attr("title")]
        public string Title { get; set; }
        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }
        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }

       [HasMany("media")]
       public virtual List<TaskMedia> Taskmedia { get; set; }

        [NotMapped]
        [HasManyThrough(nameof(TaskSets))]
        public List<Set> Sets { get; set; }
        public List<TaskSet> TaskSets { get; set; }

        /*
         // [HasMany("user-tasks")]
         // public virtual List<UserTask> Usertasks { get; set; }
         */

    }
}
