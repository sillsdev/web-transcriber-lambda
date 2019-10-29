using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Plan : BaseModel, IArchive
    {

        [Attr("name")]
        public string Name { get; set; }

        [Attr("slug")]
        public string Slug { get; set; }

        [HasOne("project")]
        public Project Project { get; set; }
        [Attr("project-id")]
        public int ProjectId { get; set; }

        [HasOne("owner")]
        public virtual User Owner { get; set; }
        [Attr("owner-id")]
        public int OwnerId { get; set; }


        [HasOne("plantype")]
        public PlanType Plantype { get; set; }
        [Attr("plantype-id")]
        public int PlantypeId { get; set; }
        [HasMany("sections")]
        public virtual List<Section> Sections { get; set; }

        [HasMany("mediafiles")]
        public virtual List<Mediafile> Mediafiles { get; set; }
        public bool Archived { get; set; }

    }
}
