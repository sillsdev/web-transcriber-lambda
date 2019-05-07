using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Plan : BaseModel
    {

        [Attr("name")]
        public string Name { get; set; }
        [HasOne("project")]
        public Project Project { get; set; }
        [Attr("project-id")]
        public int ProjectId { get; set; }

        [HasOne("type")]
        public PlanType Plantype { get; set; }
        [Attr("plan-type-id")]
        public int PlanTypeId { get; set; }

        [HasMany("sections")]
        public virtual List<Section> Sections { get; set; }
    }
}
