using JsonApiDotNetCore.Models;
using System;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public class ProjectType : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [HasMany("projects")]
        public virtual List<Project> Projects { get; set; }
    }
}
