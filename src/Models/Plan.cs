using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    [Table("plans")]

    public partial class Plan : BaseModel, IArchive
    {
        public Plan():base()
        {
            Name = "";
            Project = new Project();
            Plantype = new PlanType();
        }
        [Attr(PublicName="name")]
        public string Name { get; set; }

        [Attr(PublicName="slug")]
        public string? Slug { get; set; }

        [Attr(PublicName="organized-by")]
        public string? OrganizedBy { get; set; }

        [Attr(PublicName="tags")]
        [Column(TypeName = "jsonb")]
        public string? Tags { get; set; }

        [Attr(PublicName="flat")]
        public bool Flat { get; set; }

        [Attr(PublicName="section-count")]
        public int SectionCount { get; set; }

       // [Attr(PublicName = "project-id")]
        public int ProjectId { get; set; }

        [HasOne(PublicName="project")]
        public Project Project { get; set; }

        [Attr(PublicName="owner-id")]
        public int? OwnerId { get; set; }

        [HasOne(PublicName="owner")]
        public User? Owner { get; set; }

        [Attr(PublicName = "plantype-id")]
        public int PlantypeId { get; set; }

        [HasOne(PublicName = "plantype")]
        public PlanType Plantype { get; set; }

        [HasMany(PublicName = "sections")]
        public List<Section>? Sections { get; set; }
        public bool Archived { get; set; }

    }
}
