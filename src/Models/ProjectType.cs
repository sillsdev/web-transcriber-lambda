﻿using JsonApiDotNetCore.Models;
using System.Collections.Generic;

namespace SIL.Transcriber.Models
{
    public partial class ProjectType : BaseModel
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [HasMany("projects", Link.None)]
        public virtual List<Project> Projects { get; set; }
    }
}
