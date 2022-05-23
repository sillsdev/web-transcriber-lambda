
using JsonApiDotNetCore.Resources.Annotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class WorkflowStepBase : BaseModel, IArchive
    {
        public WorkflowStepBase(): base()
        {
            Process = "";
            Name = "";
            Tool = "{}";
            Permissions = "{}";
        }
        [Attr(PublicName = "process")]
        public string Process { get; set; }

        [Attr(PublicName = "name")]
        public string Name { get; set; }
        
        [Attr(PublicName = "sequencenum")]
        public int Sequencenum { get; set; }

        [Attr(PublicName = "tool")]
        [Column(TypeName = "jsonb")]
        public string Tool { get; set; }
        
        [Attr(PublicName = "permissions")]
        [Column(TypeName = "jsonb")]
        public string Permissions { get; set; }

        public bool Archived { get; set; }
    }
    [Table("workflowsteps")]
    public class Workflowstep : WorkflowStepBase { }
}
