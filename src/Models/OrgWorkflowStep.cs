using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    [Table("orgworkflowsteps")]

    public class OrgWorkflowstep : WorkflowStepBase
    {
        public int OrganizationId { get; set; }
        [HasOne(PublicName = "organization")]
        public Organization? Organization { get; set; }

        public int? ParentId { get; set; }
        [HasOne(PublicName = "parent")]
        public OrgWorkflowstep? Parent { get; set; }  
    }
}
