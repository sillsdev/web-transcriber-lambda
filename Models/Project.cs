using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class Project : BaseModel, ITrackDate
    {
        [Attr("name")]
        public string Name { get; set; }

        [HasOne("type")]
        public ProjectType Projecttype { get; set; }

        [Attr("project-type-id")]
        public int ProjectTypeId { get; set; }

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

        [Attr("language")]
        public string Language { get; set; }

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

        [Attr("allow-downloads")]
        public bool? AllowDownloads { get; set; } = true;

        [HasMany("project-integrations")]
        public virtual List<ProjectIntegration> ProjectIntegrations { get; set; }
        //[HasManyThrough("project-integrations")]
        //public virtual List<Integration> Integrations { get; set; }

        [HasMany("project-users")]
        public virtual List<ProjectUser> ProjectUsers { get; set; }

       // [HasManyThrough("users")]
        //public virtual List<User> Users { get; set; }
        //public ICollection<Projectuser> Projectusers { get; set; }

        [HasMany("sets")]
        public virtual List<Set> Sets { get; set; }

        [HasMany("tasks")]
        public virtual List<Task> Tasks { get; set; }
    }
}
