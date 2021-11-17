
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class Discussion : BaseModel, IArchive
    {
        [Attr("subject")]
        public string Subject { get; set; }
        public int MediafileId { get; set; }
        [HasOne("mediafile", Link.None)]
        public Mediafile Mediafile { get; set; }
        [Attr("segments")]
        [Column(TypeName = "jsonb")]
        public string Segments { get; set; }
        public bool Resolved { get; set; }
        public int? RoleId { get; set; }
        [HasOne("role", Link.None)]
        public Role Role { get; set; }
        public int? UserId { get; set; }
        [HasOne("user", Link.None)]
        public User User { get; set; }
        public int OrgWorkflowStepId { get; set; }
        [HasOne("org-workflow-step", Link.None)]
        public OrgWorkflowStep OrgWorkflowStep { get; set; }
        public int? ArtifactCategoryId { get; set; }
        [HasOne("artifact-category", Link.None)]
        public ArtifactCategory ArtifactCategory { get; set; }
        public bool Archived { get; set; }
    }
}
