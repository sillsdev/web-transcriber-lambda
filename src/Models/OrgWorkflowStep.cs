using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class OrgWorkflowStep : WorkflowStepBase
    {
        public int OrganizationId { get; set; }
        [Attr("organization")]
        public Organization Organization { get; set; }

        public int ParentId { get; set; }
        public OrgWorkflowStep Parent { get; set; }  
    }
}
