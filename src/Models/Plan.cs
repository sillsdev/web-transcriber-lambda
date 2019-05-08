using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Plan : BaseModel
    {

        [Attr("name")]
        public string Name { get; set; }
<<<<<<< HEAD

=======
>>>>>>> c1957ccd5d40c71a2d0ef6350db403c5cf763b22
        [HasOne("project")]
        public Project Project { get; set; }
        [Attr("project-id")]
        public int ProjectId { get; set; }

<<<<<<< HEAD
        [HasOne("plantype")]
        public PlanType Plantype { get; set; }
        [Attr("plantype-id")]
        public int PlantypeId { get; set; }
=======
        [HasOne("type")]
        public PlanType Plantype { get; set; }
        [Attr("plan-type-id")]
        public int PlanTypeId { get; set; }
>>>>>>> c1957ccd5d40c71a2d0ef6350db403c5cf763b22

        [HasMany("sections")]
        public virtual List<Section> Sections { get; set; }
    }
}
