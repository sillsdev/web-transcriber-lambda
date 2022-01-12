using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;


namespace SIL.Transcriber.Models
{
    public partial class Project : BaseModel, IArchive, IBelongsToOrganization
    {
        [Attr("name")]
        public string Name { get; set; }

        [Attr("slug")]
        public string Slug { get; set; }

        [HasOne("projecttype", Link.None)]
        public virtual ProjectType Projecttype { get; set; }
        [Attr("projecttype-id")]
        public int ProjecttypeId { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [HasOne("owner", Link.None)]
        public virtual User Owner { get; set; }
        [Attr("owner-id")]
        public int? OwnerId { get; set; }

        [HasOne("organization", Link.None)]
        public virtual Organization Organization { get; set; }
        [Attr("organization-id")]
        public int OrganizationId { get; set; }

        [HasOne("group", Link.None)]
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

        [Attr("spell-check")]
        public bool? SpellCheck { get; set; } = true;

        [Attr("date-archived")]
        public DateTime? DateArchived { get; set; }

        [HasMany("project-integrations", Link.None)]
        public virtual List<ProjectIntegration> ProjectIntegrations { get; set; }
        //[HasManyThrough(nameof(ProjectIntegrations))]
        //public virtual List<Integration> Integrations { get; set; }

        [HasMany("plans", Link.None)]
        public virtual List<Plan> Plans { get; set; }
        //[HasManyThrough("tasks")]
        //public virtual List<Task> Tasks { get; set; }
        public bool Archived { get; set; }

    }
}
