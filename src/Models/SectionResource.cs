using JsonApiDotNetCore.Resources.Annotations;
using System.Text.Json.Serialization;

namespace SIL.Transcriber.Models
{
    public partial class Sectionresource : BaseModel, IArchive
    {
        [Attr(PublicName = "sequence-num")]
        public int SequenceNum { get; set; }

        [Attr(PublicName = "description")]
        public string? Description { get; set; }

        [Attr(PublicName = "section-id")]
        public int SectionId { get; set; }

        [HasOne(PublicName = "section")]
        public Section? Section { get; set; }

        [Attr(PublicName = "mediafile-id")]
        public int? MediafileId { get; set; }

        [HasOne(PublicName = "mediafile")]
        public Mediafile? Mediafile { get; set; }

        [Attr(PublicName = "passage-id")]
        public int? PassageId { get; set; }

        [HasOne(PublicName = "passage")]
        public Passage? Passage { get; set; }

        [Attr(PublicName = "project-id")]
        public int? ProjectId { get; set; }

        [HasOne(PublicName = "project")]
        public Project? Project { get; set; }

        public int OrgWorkflowStepId { get; set; }

        [HasOne(PublicName = "org-workflow-step")]
        public Orgworkflowstep? OrgWorkflowStep { get; set; }

        public bool Archived { get; set; }
    }
}
