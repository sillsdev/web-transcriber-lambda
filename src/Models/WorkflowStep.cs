
using JsonApiDotNetCore.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace SIL.Transcriber.Models
{
    public class WorkflowStepBase : BaseModel, IArchive
    {
        [Attr("process")]
        public string Process { get; set; }

        [Attr("name")]
        public string Name { get; set; }
        
        [Attr("sequencenum")]
        public int Sequencenum { get; set; }

        [Attr("tool")]
        [Column(TypeName = "jsonb")]
        public string Tool { get; set; }
        
        [Attr("permissions")]
        [Column(TypeName = "jsonb")]
        public string Permissions { get; set; }

        public bool Archived { get; set; }
    }
    public class WorkflowStep : WorkflowStepBase { }
}
