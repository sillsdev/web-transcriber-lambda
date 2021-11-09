using JsonApiDotNetCore.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace SIL.Transcriber.Models
{
    public partial class SectionResource : BaseModel, IArchive
    {
        [Attr("section-id")]
        public int SectionId { get; set; }

        [HasOne("section", Link.None)]
        public Section Section { get; set; }
        [Attr("mediafile-id")]
        public int MediafileId { get; set; }

        [HasOne("mediafile", Link.None)]
        public Mediafile Mediafile { get; set; }

        [HasOne("steps", Link.None)]
        public int[] Steps { get; set; }

        [HasMany("section-resource-org-workflow-steps", Link.None)]
        public List<SectionResourceOrgWorkflowStep> SectionResourceOrgWorkflowSteps { get; set; }

        [NotMapped]
        [HasMany("org-workflow-steps", Link.None)]
        public IEnumerable<OrgWorkflowStep> OrgWorkflowSteps => SectionResourceOrgWorkflowSteps?.Where(sr => !sr.Archived).Select(sr => sr.OrgWorkflowStep);

        [HasMany("section-resource-users", Link.None)]
        public List<SectionResourceUser> SectionResourceUsers { get; set; }

        public bool Archived { get; set; }
    }
}
