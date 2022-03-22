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

        [Attr("organized-by")]
        public string OrganizedBy { get; set; }

        [Attr("tags")]
        [Column(TypeName = "jsonb")]
        public string Tags { get; set; }

        [Attr("flat")]
        public bool Flat { get; set; }

        [Attr("section-count")]
        public int SectionCount { get; set; }

        [HasOne("project", Link.None)]
        public Project Project { get; set; }
        [Attr("project-id")]
        public int ProjectId { get; set; }

        [HasOne("owner", Link.None)]
        public virtual User Owner { get; set; }
        [Attr("owner-id")]
        public int? OwnerId { get; set; }

        [HasOne("plantype", Link.None)]
        public PlanType Plantype { get; set; }
        [Attr("plantype-id")]
        public int PlantypeId { get; set; }
        [HasMany("sections", Link.None)]
        public virtual List<Section> Sections { get; set; }

        [HasMany("mediafiles", Link.None)]
        public virtual List<Mediafile> Mediafiles { get; set; }
        public bool Archived { get; set; }

    }
}
