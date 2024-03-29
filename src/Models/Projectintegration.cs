﻿using JsonApiDotNetCore.Resources.Annotations;
using SIL.Transcriber.Data;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table(Tables.ProjectIntegrations)]

    public partial class Projectintegration : BaseModel, IArchive
    {
        [Attr(PublicName = "project-id")]
        public int ProjectId { get; set; }
        [Attr(PublicName = "integration-id")]
        public int IntegrationId { get; set; }

        [HasOne(PublicName = "integration")]
        public virtual Integration? Integration { get; set; }
        [HasOne(PublicName = "project")]
        public virtual Project? Project { get; set; }

        [Attr(PublicName = "settings")]
        [Column(TypeName = "jsonb")]
        public string? Settings { get; set; }

        public bool Archived { get; set; }

    }
}
