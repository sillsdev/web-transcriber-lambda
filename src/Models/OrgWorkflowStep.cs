using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class OrgWorkflowStep : WorkflowStepBase
    {
        public int OrganizationId { get; set; }
        [HasOne("organization", Link.None)]
        public Organization Organization { get; set; }

        public int? ParentId { get; set; }
        [HasOne("parent", Link.None)]
        public OrgWorkflowStep Parent { get; set; }  
    }
}
