using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources.Annotations;

namespace SIL.Transcriber.Models
{
    public class Discussion : BaseModel, IArchive
    {
        [Attr(PublicName = "subject")]
        public string? Subject { get; set; }
        public int? MediafileId { get; set; }

        [HasOne(PublicName = "mediafile")]
        public Mediafile? Mediafile { get; set; }

        [Attr(PublicName = "segments")]
        [Column(TypeName = "jsonb")]
        public string? Segments { get; set; }

        [Attr(PublicName = "resolved")]
        public bool Resolved { get; set; }
        public int? RoleId { get; set; }

        [HasOne(PublicName = "role")]
        public Role? Role { get; set; }
        public int? GroupId { get; set; }

        [HasOne(PublicName = "group")]
        public Group? Group { get; set; }
        public int? UserId { get; set; }

        [HasOne(PublicName = "user")]
        public User? User { get; set; }
        public int OrgWorkflowStepId { get; set; }

        [HasOne(PublicName = "org-workflow-step")]
        public Orgworkflowstep? OrgWorkflowStep { get; set; }
        public int? ArtifactCategoryId { get; set; }

        [HasOne(PublicName = "artifact-category")]
        public Artifactcategory? ArtifactCategory { get; set; }

        [Attr(PublicName = "offline-id")]
        public string? OfflineId { get; set; }

        [Attr(PublicName = "offline-mediafile-id")]
        public string? OfflineMediafileId { get; set; }
        public bool Archived { get; set; }
    }
}
