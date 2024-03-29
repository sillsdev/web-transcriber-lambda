﻿using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    [Table(Tables.Plans)]
    public partial class Plan : BaseModel, IArchive
    {
        [Attr(PublicName = "name")]
        public string Name { get; set; } = "";

        [Attr(PublicName = "slug")]
        public string? Slug { get; set; }

        [Attr(PublicName = "organized-by")]
        public string? OrganizedBy { get; set; }

        [Attr(PublicName = "tags")]
        [Column(TypeName = "jsonb")]
        public string? Tags { get; set; }

        [Attr(PublicName = "flat")]
        public bool Flat { get; set; }

        [Attr(PublicName = "section-count")]
        public int SectionCount { get; set; }

        // [Attr(PublicName = "project-id")]
        public int ProjectId { get; set; }

        [HasOne(PublicName = "project")]
        public Project Project { get; set; } = null!;

        [Attr(PublicName = "owner-id")]
        public int? OwnerId { get; set; }

        [HasOne(PublicName = "owner")]
        public User? Owner { get; set; }

        [Attr(PublicName = "plantype-id")]
        public int PlantypeId { get; set; }

        [HasOne(PublicName = "plantype")]
        public Plantype Plantype { get; set; } = null!;

        public bool Archived { get; set; }
    }
}
