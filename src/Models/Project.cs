﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Project : BaseModel, ITrackDate, IBelongsToOrganization
    {
        [Attr("name")]
        public string Name { get; set; }

        [NotMapped]
        public string Slug { get => "Org" + Id.ToString(); }

        [HasOne("projecttype")]
        public virtual ProjectType Projecttype { get; set; }
        [Attr("projecttype-id")]
        public int ProjecttypeId { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [HasOne("owner")]
        public virtual User Owner { get; set; }
        [Attr("owner-id")]
        public int OwnerId { get; set; }

        [HasOne("organization")]
        public virtual Organization Organization { get; set; }
        [Attr("organization-id")]
        public int OrganizationId { get; set; }

        [HasOne("group")]
        public virtual Group Group { get; set; }
        [Attr("group-id")]
        public int GroupId { get; set; }

        //settings
        [Attr("uilanguagebcp47")]
        public string Uilanguagebcp47 { get; set; }

        [Attr("language")]
        public string Language { get; set; }

        [Attr("language-name")]
        public string LanguageName { get; set; }

        [Attr("default-font")]
        public string DefaultFont { get; set; }

        [Attr("default-font-size")]
        public string DefaultFontSize { get; set; }

        [Attr("rtl")]
        public bool? Rtl { get; set; } = true;

        [Attr("allow-claim")]
        public bool? AllowClaim { get; set; } = true;

        [Attr("is-public")]
        public bool? IsPublic { get; set; } = true;

        [Attr("date-created")]
        public DateTime? DateCreated { get; set; }

        [Attr("date-updated")]
        public DateTime? DateUpdated { get; set; }

        [Attr("date-archived")]
        public DateTime? DateArchived { get; set; }

        [HasMany("reviewers", Link.None)]
        public virtual List<Reviewer> Reviewers { get; set; }

        [HasMany("project-integrations")]
        public virtual List<ProjectIntegration> ProjectIntegrations { get; set; }
        //[HasManyThrough(nameof(ProjectIntegrations))]
        //public virtual List<Integration> Integrations { get; set; }

        [HasMany("plans")]
        public virtual List<Plan> Plans { get; set; }
        //[HasManyThrough("tasks")]
        //public virtual List<Task> Tasks { get; set; }

    }
}
