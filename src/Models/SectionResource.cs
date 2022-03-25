using JsonApiDotNetCore.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class SectionResource : BaseModel, IArchive
    {
        [Attr("sequence-num")]
        public int SequenceNum { get; set; }

        [Attr("description")]
        public string Description { get; set; }

        [Attr("section-id")]
        public int SectionId { get; set; }

        [HasOne("section", Link.None)]
        public Section Section { get; set; }
        [Attr("mediafile-id")]
        public int? MediafileId { get; set; }

        [HasOne("mediafile", Link.None)]
        public Mediafile Mediafile { get; set; }

        [Attr("passage-id")]
        public int? PassageId { get; set; }
        [HasOne("passage", Link.None)]
        public Passage Passage { get; set; }

        [Attr("project-id")]
        public int? ProjectId { get; set; }
        [HasOne("project", Link.None)]
        public Project Project { get; set; }

        public int orgWorkflowStepId { get; set; }
        [HasOne("org-workflow-step", Link.None)]
        public OrgWorkflowStep OrgWorkflowStep { get; set; }

        [HasMany("section-resource-users", Link.None)]
        public List<SectionResourceUser> SectionResourceUsers { get; set; }

        public bool Archived { get; set; }
    }
}
