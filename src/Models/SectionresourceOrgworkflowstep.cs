using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Models;

namespace SIL.Transcriber.Models
{
    public partial class SectionResourceOrgWorkflowStep : BaseModel, IArchive
    {

        [HasOne("section-resource", Link.None)]
        public virtual SectionResource SectionResource { get; set; }

        //[Attr("section-resource-id")]
        public int SectionResourceId { get; set; }

        [HasOne("org-workflow-step", Link.None)]
        public virtual OrgWorkflowStep OrgWorkflowStep { get; set; }

        //[Attr("org-workflow-step-id")]
        public int OrgWorkflowStepId { get; set; }

        public bool Archived { get; set; }
    }
}
