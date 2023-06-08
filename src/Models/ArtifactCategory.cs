﻿using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.ArtifactCategorys)]
    public class Artifactcategory : BaseModel, IArchive
    {
        [Attr(PublicName = "categoryname")]
        public string? Categoryname { get; set; }
        [Attr(PublicName = "discussion")]
        public bool Discussion { get; set; }
        [Attr(PublicName = "resource")]
        public bool Resource { get; set; }
        public bool Archived { get; set; }
        public int? OrganizationId { get; set; }
        [HasOne(PublicName = "organization")]
        public Organization? Organization { get; set; }
    }
}
